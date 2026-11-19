using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Voxels.Tests
{
    /// <summary>The corpse pool (VoxelCorpses): bodies made at level load, taken by deaths, put away with their corpse.</summary>
    public sealed class VoxelCorpsesTests
    {
        private static readonly int3[] OneChunk = { new(1, 1, 1) };
        private static readonly string[] Names = { "Flesh_0", "Flesh_1" };

        private readonly List<VoxelCarcass.Piece> _pieces = new(4);

        [SetUp]
        public void SetUp()
        {
            VoxelCorpses.Clear();
            VoxelBody.PieceFactory = null;
        }

        [TearDown]
        public void TearDown()
        {
            VoxelCarcass.Release(_pieces);
            VoxelCorpses.Clear();
            VoxelCorpses.Capacity = 20;
            VoxelBody.PieceFactory = null;
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private static VoxelBodySettings Settings()
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Layer = VoxelWorld.PieceLayer;
            settings.ChunkSize = 32;
            settings.ColliderCell = 2;
            settings.Settle = true;
            settings.KeepDebris = true;
            return settings;
        }

        /// <summary>The pool made ready for the active scene and filled, as a level load does over its frames.</summary>
        private static IEnumerator Ready(int corpses, int bodies, int3[] layouts = null)
        {
            VoxelCorpses.Prepare(SceneManager.GetActiveScene(), corpses, bodies, Settings(), layouts ?? OneChunk, 16);
            for (int frame = 0; VoxelCorpses.ToPrime > 0 && frame < 100; frame++)
            {
                VoxelCorpses.Prime(8);
                yield return null;
            }

            Assert.AreEqual(0, VoxelCorpses.ToPrime, "filled");
        }

        /// <summary>
        /// A grid in parts, as SkinnedVoxelizer gives: <paramref name="partAt"/> is the part of each voxel, -1 empty.
        /// The colours are the part's and the voxel's place, so two grids that differ show it.
        /// </summary>
        private static SkinnedVoxelizer.Result Grid(int3 size, Func<int3, int> partAt, byte shade = 0)
        {
            int count = size.x * size.y * size.z;
            var grid = new SkinnedVoxelizer.Result
            {
                Size = size,
                Origin = float3.zero,
                VoxelSize = 0.05f,
                Position = new Vector3(0f, 2f, 0f),
                Rotation = Quaternion.identity,
                Voxels = new NativeArray<byte>(count, Allocator.Persistent),
                Parts = new NativeArray<byte>(count, Allocator.Persistent),
                Palette = new NativeArray<Color32>(256, Allocator.Persistent)
            };
            for (int i = 1; i < 8; i++)
                grid.Palette[i] = new Color32((byte)(30 * i + shade), (byte)(200 - 20 * i), (byte)(shade + 9), 255);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                int index = x + size.x * (y + size.y * z);
                int part = partAt(new int3(x, y, z));
                grid.Parts[index] = part < 0 ? SkinnedVoxelizer.NO_PART : (byte)part;
                grid.Voxels[index] = part < 0 ? (byte)0 : (byte)(1 + part * 3 + (x + y + z) % 3);
            }

            return grid;
        }

        /// <summary>Two 10³ blocks side by side, parts 0 and 1.</summary>
        private static SkinnedVoxelizer.Result TwoBlocks() =>
            Grid(new int3(20, 10, 10), p => p.x < 10 ? 0 : 1);

        /// <summary>One part: a 10³ block and, apart from it, a 4³ scrap that splits off.</summary>
        private static SkinnedVoxelizer.Result BlockAndScrap() =>
            Grid(new int3(18, 10, 10), p => p.x < 10 || (p.x >= 14 && p.y < 4 && p.z < 4) ? 0 : -1);

        private static HashSet<VoxelBody> FreeBodies() =>
            new(Object.FindObjectsByType<VoxelBody>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.name == VoxelCorpses.POOLED_NAME && b.IsRecycled));

        private VoxelCorpses.Corpse Build(in SkinnedVoxelizer.Result grid, int[] clusters)
        {
            VoxelCarcass.Release(_pieces);
            VoxelCorpses.Corpse corpse = VoxelCorpses.Begin();
            VoxelCarcass.Build(grid, clusters, Settings(), Names, true, _pieces, corpse);
            return corpse;
        }

        /// <summary>Shown as VoxelGibs shows a death: switched on, launched and split, its scraps the corpse's.</summary>
        private void Show(VoxelCorpses.Corpse corpse)
        {
            using (VoxelCorpses.Serve(corpse))
            {
                for (int i = 0; i < _pieces.Count; i++)
                {
                    VoxelBody body = _pieces[i].Body;
                    body.gameObject.SetActive(true);
                    body.Launch(Vector3.up, Vector3.zero);
                    body.SplitLoose();
                }
            }

            VoxelCorpses.Seal(corpse);
        }

        [UnityTest]
        public IEnumerator ADeath_TakesTheBodiesMadeAhead_AndTheyGoBackWithItsCorpse()
        {
            yield return Ready(4, 4);
            HashSet<VoxelBody> free = FreeBodies();
            Assert.AreEqual(4, free.Count);
            Assert.AreEqual(4, VoxelCorpses.FreeCount);
            Assert.That(free.All(b => !b.gameObject.activeSelf && b.GetComponent<Rigidbody>() != null),
                "made with what a loose piece carries, switched off");
            Assert.That(free.All(b => b.GetComponentsInChildren<BoxCollider>(true).Length >= 16), "and the boxes of a chunk");

            using SkinnedVoxelizer.Result grid = TwoBlocks();
            VoxelCorpses.Corpse corpse = Build(grid, new[] { 0, 1 });

            Assert.AreEqual(2, corpse.BodyCount);
            Assert.AreEqual(2, VoxelCorpses.FreeCount);
            for (int i = 0; i < _pieces.Count; i++)
            {
                VoxelBody body = _pieces[i].Body;
                Assert.That(free, Has.Member(body), "a body made ahead, not a new one");
                Assert.IsTrue(corpse.Holds(body));
                Assert.IsFalse(body.IsRecycled);
                Assert.AreEqual(Names[_pieces[i].Cluster], body.name);
                Assert.AreEqual(1000, body.SolidCount);
                Assert.IsFalse(body.gameObject.activeSelf, "switched off until shown");
            }

            Show(corpse);
            yield return null;
            VoxelBody[] taken = _pieces.Select(p => p.Body).ToArray();
            Assert.That(taken.All(b => b.gameObject.activeSelf && b.IsLoose));

            VoxelCorpses.Abandon(corpse);
            Assert.AreEqual(4, VoxelCorpses.FreeCount);
            Assert.AreEqual(0, VoxelCorpses.CorpseCount);
            Assert.That(taken.All(b => b.IsRecycled && !b.gameObject.activeSelf && b.name == VoxelCorpses.POOLED_NAME));
        }

        [UnityTest]
        public IEnumerator AFullPool_PutsTheOldestCorpseAway_AndTheNextDeathTakesItsBodies()
        {
            yield return Ready(2, 4);
            using SkinnedVoxelizer.Result grid = TwoBlocks();

            VoxelCorpses.Corpse first = Build(grid, new[] { 0, 1 });
            Show(first);
            VoxelBody[] firstBodies = _pieces.Select(p => p.Body).ToArray();
            VoxelCorpses.Corpse second = Build(grid, new[] { 0, 1 });
            Show(second);
            VoxelBody[] secondBodies = _pieces.Select(p => p.Body).ToArray();
            yield return null;
            Assert.AreEqual(2, VoxelCorpses.CorpseCount);
            Assert.AreEqual(0, VoxelCorpses.FreeCount);

            VoxelCorpses.Corpse third = Build(grid, new[] { 0, 1 });
            Show(third);
            VoxelBody[] thirdBodies = _pieces.Select(p => p.Body).ToArray();

            Assert.AreEqual(2, VoxelCorpses.CorpseCount, "as many as may lie");
            CollectionAssert.AreEquivalent(firstBodies, thirdBodies, "the oldest corpse's bodies");
            Assert.That(secondBodies.All(b => !b.IsRecycled && b.gameObject.activeSelf && b.SolidCount == 1000),
                "the younger corpse still lies");
            Assert.That(thirdBodies.All(b => b.SolidCount == 1000));
        }

        [UnityTest]
        public IEnumerator ScrapsSplitOffACorpse_AreItsOwn_AndGoBackWithIt()
        {
            yield return Ready(4, 4);
            using SkinnedVoxelizer.Result grid = BlockAndScrap();
            VoxelCorpses.Corpse corpse = Build(grid, new[] { 0 });
            Assert.AreEqual(1, corpse.BodyCount);

            Show(corpse);

            Assert.AreEqual(2, corpse.BodyCount, "the scrap took a body of the corpse");
            VoxelBody scrap = corpse.GetBody(1);
            Assert.AreEqual(64, scrap.SolidCount);
            Assert.AreEqual(1000, _pieces[0].Body.SolidCount);
            Assert.AreEqual("VoxelPiece", scrap.name, "named as VoxelBody names its pieces");
            Assert.AreEqual(2, VoxelCorpses.FreeCount);
            Assert.IsNull(VoxelBody.PieceFactory, "a split elsewhere makes its own pieces again");
            yield return null;
            Assert.IsTrue(scrap.gameObject.activeSelf && scrap.IsLoose);

            VoxelCorpses.Abandon(corpse);
            Assert.AreEqual(4, VoxelCorpses.FreeCount);
            Assert.IsTrue(scrap.IsRecycled);
            Assert.IsFalse(scrap.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator APooledBody_ComesOutAsANewBodyWould()
        {
            using SkinnedVoxelizer.Result grid = TwoBlocks();
            var oneOff = new List<VoxelCarcass.Piece>();
            VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), Names, true, oneOff);

            yield return Ready(4, 2);
            // The pool's bodies used once for something else (bigger, other colours), then put away.
            using (SkinnedVoxelizer.Result other = Grid(new int3(30, 12, 9), p => p.x < 15 ? 0 : 1, 77))
            {
                VoxelCorpses.Corpse before = Build(other, new[] { 0, 1 });
                Show(before);
                yield return null;
                VoxelCorpses.Abandon(before);
            }

            VoxelCorpses.Corpse corpse = Build(grid, new[] { 0, 1 });
            Assert.AreEqual(oneOff.Count, _pieces.Count);
            for (int i = 0; i < _pieces.Count; i++)
            {
                VoxelBody a = oneOff[i].Body;
                VoxelBody b = _pieces[i].Body;
                Assert.IsTrue(corpse.Holds(b));
                Assert.AreEqual(a.name, b.name);
                Assert.AreEqual(a.Size, b.Size);
                Assert.AreEqual(a.Origin, b.Origin);
                Assert.AreEqual(a.SolidCount, b.SolidCount);
                Assert.AreEqual(a.transform.position, b.transform.position);
                Assert.AreEqual(oneOff[i].TornCount, _pieces[i].TornCount);
                for (int f = 0; f < _pieces[i].TornCount; f++)
                    Assert.AreEqual(oneOff[i].Torn[f].Point, _pieces[i].Torn[f].Point);
                SameVoxels(a, b);
            }

            // Built (the world, at the end of the frame): the same meshes and the same boxes.
            for (int i = 0; i < _pieces.Count; i++)
            {
                oneOff[i].Body.gameObject.SetActive(true);
                _pieces[i].Body.gameObject.SetActive(true);
            }

            yield return null;
            for (int i = 0; i < _pieces.Count; i++)
            {
                VoxelBody a = oneOff[i].Body;
                VoxelBody b = _pieces[i].Body;
                Assert.AreEqual(Vertices(a), Vertices(b), "drawn the same");
                CollectionAssert.AreEquivalent(Boxes(a), Boxes(b), "colliding the same");
            }

            VoxelCarcass.Release(oneOff);
        }

        /// <summary>The same colour voxel by voxel, sampled through the grid.</summary>
        private static void SameVoxels(VoxelBody a, VoxelBody b)
        {
            for (int z = 0; z < a.Size.z; z++)
            for (int y = 0; y < a.Size.y; y++)
            for (int x = 0; x < a.Size.x; x++)
            {
                Vector3 p = a.transform.TransformPoint((Vector3)(a.Origin + (new float3(x, y, z) + 0.5f) * a.VoxelSize));
                bool inA = a.TryGetColor(p, out Color32 ca);
                bool inB = b.TryGetColor(p, out Color32 cb);
                Assert.AreEqual(inA, inB, $"voxel {x},{y},{z}");
                if (inA)
                    Assert.AreEqual(ca, cb, $"colour of {x},{y},{z}");
            }
        }

        private static int Vertices(VoxelBody body) =>
            body.GetComponentsInChildren<MeshFilter>().Where(f => f.gameObject.activeInHierarchy).Sum(f => f.sharedMesh.vertexCount);

        private static List<Bounds> Boxes(VoxelBody body) =>
            body.GetComponentsInChildren<BoxCollider>().Where(b => b.enabled).Select(b => new Bounds(b.center, b.size)).ToList();

        [UnityTest]
        public IEnumerator APooledCorpse_AfterTheFirst_AllocatesNothing()
        {
            yield return Ready(1, 3);
            using SkinnedVoxelizer.Result grid = BlockAndScrap();
            int[] clusters = { 0 };

            // Once through: the code compiled, the lists grown, the bodies given what this corpse takes.
            Cycle(grid, clusters);
            yield return null;

            // The pool holds one corpse: this one puts the last away and takes its bodies.
            Assert.That(() => Cycle(grid, clusters), NoAllocation.Constraint,
                "a corpse built, shown, split and built by the world from the pool's bodies");
            Assert.AreEqual(1, VoxelCorpses.CorpseCount);

            // The control: the same pieces as new bodies do allocate, so the count above sees garbage.
            var oneOff = new List<VoxelCarcass.Piece>(4);
            Assert.That(() => VoxelCarcass.Build(grid, clusters, Settings(), Names, true, oneOff), NoAllocation.Allocates);
            VoxelCarcass.Release(oneOff);
        }

        private void Cycle(in SkinnedVoxelizer.Result grid, int[] clusters)
        {
            VoxelCorpses.Corpse corpse = Build(grid, clusters);
            Show(corpse);
            VoxelWorld.Instance.VoxelLateUpdate(0f);
        }

        [UnityTest]
        public IEnumerator EachLevel_MakesItsOwnBodies_AndLetsThemGoWithIt()
        {
            Scene test = SceneManager.GetActiveScene();
            Scene level = SceneManager.CreateScene("CorpsesLevel");
            SceneManager.SetActiveScene(level);
            yield return Ready(2, 3);

            Assert.IsTrue(VoxelCorpses.IsPreparedFor(level));
            Assert.IsFalse(VoxelCorpses.IsPreparedFor(test));
            HashSet<VoxelBody> made = FreeBodies();
            Assert.AreEqual(3, made.Count);
            Assert.That(made.All(b => b.gameObject.scene == level), "objects of the level's scene");

            SceneManager.SetActiveScene(test);
            yield return SceneManager.UnloadSceneAsync(level);
            Assert.That(made.All(b => b == null), "gone with the level");

            // The next level makes its own.
            yield return Ready(2, 3);
            Assert.IsTrue(VoxelCorpses.IsPreparedFor(test));
            Assert.AreEqual(3, VoxelCorpses.FreeCount);
            Assert.That(FreeBodies().All(b => b.gameObject.scene == test));

            using SkinnedVoxelizer.Result grid = TwoBlocks();
            VoxelCorpses.Corpse corpse = Build(grid, new[] { 0, 1 });
            Assert.That(_pieces.All(p => p.Body != null && p.Body.gameObject.scene == test && corpse.Holds(p.Body)));
            Assert.AreEqual(1, VoxelCorpses.FreeCount);
        }

        [UnityTest]
        public IEnumerator Bodies_AreFilledInEveryPlannedLayout()
        {
            int3[] layouts = { new(1, 1, 1), new(2, 1, 1), new(1, 2, 2) };
            yield return Ready(3, 3, layouts);

            int[] chunks = FreeBodies().Select(b => b.GetComponentsInChildren<MeshFilter>(true).Length).OrderBy(c => c).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, chunks, "every chunk of the layout made");
        }
    }
}
