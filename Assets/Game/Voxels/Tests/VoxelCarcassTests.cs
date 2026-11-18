using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelCarcassTests
    {
        private static readonly Color32[] Green = { new(40, 190, 10, 30) };

        private Transform _root;
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private static VoxelBodySettings Settings()
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Settle = true;
            settings.KeepDebris = true;
            return settings;
        }

        private SkinnedVoxelizer.Result Grid()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            var parts = new[]
            {
                new SkinnedVoxelizer.Part { Bones = new[] { lower } },
                new SkinnedVoxelizer.Part { Bones = new[] { upper } }
            };
            Assert.IsTrue(SkinnedVoxelizer.Voxelize(skin, parts, new Plane(Vector3.right, 0f), 0.05f,
                SkinnedVoxelizerTests.Palette(), 1, out SkinnedVoxelizer.Result grid));
            return grid;
        }

        private void Floor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _spawned.Add(floor);
            Physics.SyncTransforms();
        }

        [Test]
        public void TwoClusters_TwoBodiesTornWhereTheyMet()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            int solid = grid.Voxels.Count(v => v != 0);

            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), "Flesh");

            Assert.AreEqual(2, pieces.Count);
            Assert.AreEqual(solid, pieces.Sum(p => p.Body.SolidCount));
            VoxelCarcass.Piece bottom = pieces.Single(p => p.Cluster == 0);
            VoxelCarcass.Piece top = pieces.Single(p => p.Cluster == 1);
            Assert.That(bottom.Torn.Length, Is.GreaterThan(0));
            Assert.That(bottom.Torn.All(f => f.Normal.y > 0.9f), "the bottom was torn on its top face");
            Assert.That(top.Torn.Length, Is.GreaterThan(0));
            Assert.That(top.Torn.All(f => f.Normal.y < -0.9f));
            Assert.That(bottom.Torn.All(f => Mathf.Abs(f.Point.y - 0.5f) < 0.06f), "at half height");
        }

        [Test]
        public void OneCluster_OneWholeBodyWithoutTears()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 0 }, Settings(), "Flesh");

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(2, pieces[0].Parts);
            Assert.IsEmpty(pieces[0].Torn);
        }

        [Test]
        public void Body_StandsWhereTheColumnStood()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 0 }, Settings(), "Flesh");

            Bounds bounds = pieces[0].Body.WorldBounds;
            Assert.That(bounds.center.y, Is.EqualTo(0.5f).Within(0.08f));
            Assert.That(bounds.size.y, Is.EqualTo(1f).Within(0.12f));
        }

        [UnityTest]
        public IEnumerator Spray_DropsLandAndLieForGood()
        {
            Floor();
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = LyingAround(debris, 3f);

            Assert.AreEqual(12, VoxelSpray.Burst(new Vector3(3f, 1f, 0f), Vector3.up, 12, 1f, 2f, 0.3f, Green, 0.025f));

            float until = Time.time + 4f;
            while (LyingAround(debris, 3f) < before + 12 && Time.time < until)
                yield return null;
            Assert.AreEqual(before + 12, LyingAround(debris, 3f));
        }

        /// <summary>Lying drops near x: other tests' drops still landing elsewhere must not count.</summary>
        private static int LyingAround(VoxelDebris debris, float x)
        {
            int count = 0;
            for (int slot = 0; slot < debris.LyingCount; slot++)
            {
                if (!debris.TryGetLying(slot, out Matrix4x4 m, out _))
                    continue;
                Vector3 p = m.GetPosition();
                if (Mathf.Abs(p.x - x) < 1.5f && Mathf.Abs(p.z) < 1.5f)
                    count++;
            }

            return count;
        }

        [Test]
        public void PooledBuild_GivesTheSameBodiesAsTheOneOff_SwitchedOff_AndReusesItsPieces()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            List<VoxelCarcass.Piece> oneOff = VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), "Flesh");
            var pooled = new List<VoxelCarcass.Piece>();
            VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), new[] { "Flesh_0", "Flesh_1" }, true, pooled);

            Assert.AreEqual(oneOff.Count, pooled.Count);
            for (int i = 0; i < pooled.Count; i++)
            {
                VoxelCarcass.Piece a = oneOff[i];
                VoxelCarcass.Piece b = pooled[i];
                Assert.AreEqual(a.Body.name, b.Body.name);
                Assert.IsTrue(a.Body.gameObject.activeSelf);
                Assert.IsFalse(b.Body.gameObject.activeSelf, "made switched off, to show later");
                Assert.AreEqual(a.Cluster, b.Cluster);
                Assert.AreEqual(a.Parts, b.Parts);
                Assert.AreEqual(a.Body.Size, b.Body.Size);
                Assert.AreEqual(a.Body.Origin, b.Body.Origin);
                Assert.AreEqual(a.Body.SolidCount, b.Body.SolidCount);
                Assert.AreEqual(a.Torn.Length, b.TornCount);
                for (int f = 0; f < b.TornCount; f++)
                {
                    Assert.AreEqual(a.Torn[f].Point, b.Torn[f].Point);
                    Assert.AreEqual(a.Torn[f].Normal, b.Torn[f].Normal);
                }

                // The same colours voxel by voxel, sampled through the bodies' bounds.
                Bounds bounds = a.Body.WorldBounds;
                for (float x = bounds.min.x; x < bounds.max.x; x += 0.05f)
                for (float y = bounds.min.y; y < bounds.max.y; y += 0.05f)
                for (float z = bounds.min.z; z < bounds.max.z; z += 0.05f)
                {
                    var p = new Vector3(x + 0.025f, y + 0.025f, z + 0.025f);
                    bool inA = a.Body.TryGetColor(p, out Color32 ca);
                    bool inB = b.Body.TryGetColor(p, out Color32 cb);
                    Assert.AreEqual(inA, inB, $"voxel at {p}");
                    if (inA)
                        Assert.AreEqual(ca, cb, $"colour at {p}");
                }
            }

            VoxelCarcass.Piece first = pooled[0];
            VoxelCarcass.Release(pooled);
            Assert.IsEmpty(pooled);
            Assert.IsNull(first.Body, "a released piece lets go of its body");

            VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), new[] { "Flesh_0", "Flesh_1" }, true, pooled);
            Assert.That(pooled, Has.Some.SameAs(first), "the next death takes the released pieces");
            VoxelCarcass.Release(pooled);
        }

        [Test]
        public void ACorpsesPieces_TakeBodiesWithWhatALoosePieceCarries()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            VoxelCorpses.Clear();
            VoxelCorpses.Corpse corpse = VoxelCorpses.Begin();

            var pooled = new List<VoxelCarcass.Piece>();
            VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), new[] { "Flesh_0", "Flesh_1" }, true, pooled, corpse);

            Assert.AreEqual(2, corpse.BodyCount, "both pieces are the corpse's");
            foreach (VoxelCarcass.Piece piece in pooled)
            {
                Assert.IsTrue(corpse.Holds(piece.Body));
                Assert.AreEqual("Flesh_" + piece.Cluster, piece.Body.name);
                Assert.AreEqual(VoxelWorld.PieceLayer, piece.Body.gameObject.layer);
                Assert.IsFalse(piece.Body.gameObject.activeSelf);
                Assert.That(piece.Body.SolidCount, Is.GreaterThan(0));
                Assert.IsNotNull(piece.Body.GetComponent<VoxelSettle>());
                Assert.IsNotNull(piece.Body.GetComponent<VoxelImpactFracture>());
            }

            VoxelCarcass.Release(pooled);
            VoxelCorpses.Clear();
        }

        [UnityTest]
        public IEnumerator PooledDrip_DropsWhileItLasts_ThenGoesBackToThePool()
        {
            Floor();
            var go = new GameObject("Piece");
            _spawned.Add(go);
            go.transform.position = Vector3.up * 2f;
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.Count + debris.LyingCount;
            int running = VoxelDrip.Running;

            var faces = new[] { new VoxelCarcass.TornFace { Point = go.transform.position, Normal = Vector3.up } };
            VoxelDrip.Start(go.transform, faces, faces.Length, Green, 0.025f, 0.3f, 30f);
            Assert.AreEqual(running + 1, VoxelDrip.Running);
            Assert.IsNull(go.GetComponent<VoxelDrip>(), "no component on the piece");
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(running, VoxelDrip.Running, "it goes when its time is up");
            Assert.That(debris.Count + debris.LyingCount - before, Is.GreaterThanOrEqualTo(5));
        }

        [UnityTest]
        public IEnumerator Drip_DropsWhileItLastsThenGoes()
        {
            Floor();
            var go = new GameObject("Piece");
            _spawned.Add(go);
            go.transform.position = Vector3.up * 2f;
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.Count + debris.LyingCount;

            VoxelDrip drip = go.AddComponent<VoxelDrip>();
            drip.Begin(new[] { go.transform.position }, Green, 0.025f, 0.3f, 30f);
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(drip == null, "it goes when its time is up");
            Assert.That(debris.Count + debris.LyingCount - before, Is.GreaterThanOrEqualTo(5));
        }
    }
}
