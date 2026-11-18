using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class SkinnedVoxelizerTests
    {
        private Transform _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        public static FleshPalette Palette() => new()
        {
            Skin = Color.green,
            SkinNoise = 0.05f,
            Meat = new[] { Color.red },
            MeatWetness = 0.6f,
            Blood = new[] { Color.yellow },
            BloodWetness = 0.9f,
            BloodShare = 0.15f
        };

        /// <summary>The cell of a grid under a world point; <paramref name="outside"/> off the grid.</summary>
        public static byte At(in SkinnedVoxelizer.Result r, NativeArray<byte> grid, Vector3 world, byte outside)
        {
            float3 local = (float3)(Quaternion.Inverse(r.Rotation) * (world - r.Position));
            int3 cell = (int3)math.floor((local - r.Origin) / r.VoxelSize);
            if (math.any(cell < 0) || math.any(cell >= r.Size))
                return outside;
            return grid[cell.x + r.Size.x * (cell.y + r.Size.y * cell.z)];
        }

        private SkinnedVoxelizer.Result Voxelize(SkinnedVoxelizer.Part[] parts, Plane plane, SkinnedMeshRenderer skin)
        {
            Assert.IsTrue(SkinnedVoxelizer.Voxelize(skin, parts, plane, 0.05f, Palette(), 1, out SkinnedVoxelizer.Result r));
            return r;
        }

        private static SkinnedVoxelizer.Part[] ByBone(Transform lower, Transform upper) => new[]
        {
            new SkinnedVoxelizer.Part { Bones = new[] { lower } },
            new SkinnedVoxelizer.Part { Bones = new[] { upper } }
        };

        [Test]
        public void Column_FillsItsVolume_SkinOutsideMeatInside()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.That(r.Voxels.Count(v => v != 0), Is.InRange(1150, 1420), "0.4 × 1 × 0.4 m in 5 cm voxels is 1280");
            Assert.That(At(r, r.Voxels, new Vector3(0.18f, 0.5f, 0f), 0),
                Is.InRange(FleshPalette.FIRST_SKIN, FleshPalette.FIRST_MEAT - 1), "the outer layer is skin");
            Assert.That(At(r, r.Voxels, new Vector3(0f, 0.5f, 0f), 0),
                Is.GreaterThanOrEqualTo(FleshPalette.FIRST_MEAT), "inside is meat or blood");
        }

        [Test]
        public void ThirtyTwoBitIndices_TwoSubmeshes_FillTheSameVolume()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            byte[] plain;
            using (SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin))
                plain = r.Voxels.ToArray();

            // Each box its own submesh; the top one's indices start from its own first vertex, the
            // submesh carrying the offset (as BakeMesh hands it on).
            Mesh mesh = skin.sharedMesh;
            int[] all = mesh.triangles;
            int half = all.Length / 2;
            int boxVertices = mesh.vertexCount / 2;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(all.Take(half).ToArray(), 0);
            mesh.SetTriangles(all.Skip(half).Select(i => i - boxVertices).ToArray(), 1, true, boxVertices);

            using SkinnedVoxelizer.Result split = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);
            CollectionAssert.AreEqual(plain, split.Voxels.ToArray());
        }

        [Test]
        public void APoseBeyondTheLargestGrid_IsNotVoxelized()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            upper.position += Vector3.right * (SkinnedVoxelizer.MAX_SIZE * 0.05f); // flung off past 160 voxels

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not voxelized"));
            Assert.IsFalse(SkinnedVoxelizer.Voxelize(skin, ByBone(lower, upper), new Plane(Vector3.right, 0f), 0.05f,
                Palette(), 1, out SkinnedVoxelizer.Result r));
            Assert.IsFalse(r.Voxels.IsCreated);
        }

        [Test]
        public void EachVoxel_GoesToThePartOfItsNearestBone()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.AreEqual(0, At(r, r.Parts, new Vector3(0f, 0.2f, 0f), SkinnedVoxelizer.NO_PART));
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(0f, 0.8f, 0f), SkinnedVoxelizer.NO_PART));
            int bottom = r.Parts.Count(p => p == 0);
            int top = r.Parts.Count(p => p == 1);
            Assert.That(Mathf.Abs(bottom - top), Is.LessThan(100), "split at half height");
        }

        [Test]
        public void SidedParts_SplitAtTheMidPlane()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            var halves = new[]
            {
                new SkinnedVoxelizer.Part { Bones = new[] { lower, upper }, Side = 1 },
                new SkinnedVoxelizer.Part { Bones = new[] { lower, upper }, Side = -1 }
            };
            using SkinnedVoxelizer.Result r = Voxelize(halves, new Plane(Vector3.right, Vector3.zero), skin);

            Assert.AreEqual(0, At(r, r.Parts, new Vector3(0.1f, 0.5f, 0f), SkinnedVoxelizer.NO_PART), "+x is the + side");
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(-0.1f, 0.5f, 0f), SkinnedVoxelizer.NO_PART));
        }

        [Test]
        public void TheGridFollowsThePose()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            upper.position += Vector3.right; // the top half moves a metre to the right
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.AreNotEqual(0, At(r, r.Voxels, new Vector3(1f, 0.75f, 0f), 0));
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(1f, 0.75f, 0f), SkinnedVoxelizer.NO_PART));
            Assert.AreEqual(0, At(r, r.Voxels, new Vector3(0f, 0.75f, 0f), 0), "nothing left where it stands");
        }

        [Test]
        public void Begin_ThenComplete_GivesTheGridVoxelizeGives_InThePoseOfTheBegin()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            SkinnedVoxelizer.Part[] parts = ByBone(lower, upper);
            var plane = new Plane(Vector3.right, 0f);
            using SkinnedVoxelizer.Result whole = Voxelize(parts, plane, skin);

            var pending = new SkinnedVoxelizer.Pending();
            Assert.IsTrue(SkinnedVoxelizer.Begin(skin, SkinnedVoxelizer.Skeleton.Of(parts), parts.Length, plane, 0.05f,
                Palette(), 1, pending));
            Assert.IsTrue(pending.Running);
            upper.position += Vector3.right; // the pose was read: moving now changes nothing
            using SkinnedVoxelizer.Result later = pending.Complete();

            Assert.IsFalse(pending.Running);
            Assert.AreEqual(whole.Size, later.Size);
            Assert.AreEqual(whole.Origin, later.Origin);
            CollectionAssert.AreEqual(whole.Voxels.ToArray(), later.Voxels.ToArray(), "the same voxels for the same seed");
            CollectionAssert.AreEqual(whole.Parts.ToArray(), later.Parts.ToArray());
            CollectionAssert.AreEqual(whole.Palette.ToArray(), later.Palette.ToArray());
        }

        [Test]
        public void Cancel_FreesAVoxelizationUnderWay()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            SkinnedVoxelizer.Part[] parts = ByBone(lower, upper);
            var pending = new SkinnedVoxelizer.Pending();
            Assert.IsTrue(SkinnedVoxelizer.Begin(skin, SkinnedVoxelizer.Skeleton.Of(parts), parts.Length,
                new Plane(Vector3.right, 0f), 0.05f, Palette(), 1, pending));

            pending.Cancel();

            Assert.IsFalse(pending.Running);
            Assert.IsFalse(pending.Complete().Voxels.IsCreated, "nothing left to hand over");
            // And it takes the next body.
            Assert.IsTrue(SkinnedVoxelizer.Begin(skin, SkinnedVoxelizer.Skeleton.Of(parts), parts.Length,
                new Plane(Vector3.right, 0f), 0.05f, Palette(), 1, pending));
            pending.Complete().Dispose();
        }
    }
}
