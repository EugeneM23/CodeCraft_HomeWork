using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>Faces lying exactly on voxel boundaries belong to the cell inside the body: no one-voxel bumps.</summary>
    public sealed class VoxelizerBoundaryTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [TestCase(false)]
        [TestCase(true)]
        public void ABoxExactlyZeroToOne_AtTenCentimetres_IsATenCubeWithAnEvenShell(bool snap)
        {
            using VoxelizerPartGrid grid = Voxelize(snap, TestMeshes.Box(Vector3.zero, Vector3.one));

            AssertBlock(grid, new int3(10, 10, 10));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ABoxOnTheGridButOffTheOrigin_WithFloatNoise_IsStillATenCube(bool snap)
        {
            // 0.3, 0.6, 0.9 are not exact in float: the corners sit a hair either side of their boundaries.
            var min = new Vector3(0.3f, 0.6f, 0.9f);
            using VoxelizerPartGrid grid = Voxelize(snap, TestMeshes.Box(min, min + Vector3.one));

            AssertBlock(grid, new int3(10, 10, 10));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoBoxesTouchingAtABoundary_GiveTwoThousand(bool snap)
        {
            using VoxelizerPartGrid grid = Voxelize(snap,
                TestMeshes.Box(Vector3.zero, Vector3.one), TestMeshes.Box(new Vector3(1f, 0f, 0f), new Vector3(2f, 1f, 1f)));

            // Both boxes' walls at x = 1 are surface too, one layer on each side: the shell is not checked.
            AssertBlock(grid, new int3(20, 10, 10), checkShell: false);
        }

        [Test]
        public void AModelOfBoxesOnAThreeCentimetreGrid_ThroughTheUseCase_HasNoBumps()
        {
            // A hull 1.2 × 0.3 × 0.6 m with a deck plate on top, and a turret part 0.3 m up carrying a 0.6 × 0.18 × 0.36 m box:
            // every face on a multiple of 3 cm. Vehicle, 1.2 m long: 3 cm voxels.
            var hull = new GameObject("Hull");
            hull.transform.SetParent(_root.transform, false);
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.zero, new Vector3(1.2f, 0.3f, 0.6f)), Color.gray);
            TestMeshes.Add(hull.transform, "Deck", TestMeshes.Box(new Vector3(0.06f, 0.3f, 0.06f), new Vector3(0.15f, 0.33f, 0.54f)), Color.gray);
            var turret = new GameObject("Turret");
            turret.transform.SetParent(hull.transform, false);
            turret.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            TestMeshes.Add(turret.transform, "Block", TestMeshes.Box(new Vector3(0.3f, 0f, 0.12f), new Vector3(0.9f, 0.18f, 0.48f)), Color.gray);

            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;
            config.parts = new[]
            {
                new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.Auto },
                new VoxelizerConfig.PartRule { path = "Hull/Turret", mark = VoxelizerConfig.PartMark.Auto }
            };
            try
            {
                using VoxelizerResult result = VoxelizeUseCase.Run(config, _root);
                Assert.AreEqual(0.03f, result.VoxelSize, 1e-6f);

                VoxelizerResultPart hullPart = result.Parts.Find(p => p.Path == "Hull");
                VoxelizerResultPart turretPart = result.Parts.Find(p => p.Path == "Hull/Turret");
                // Hull 40 × 10 × 20 and the deck 3 × 1 × 16; turret 20 × 6 × 12.
                Assert.AreEqual(40 * 10 * 20 + 3 * 1 * 16, Solid(hullPart.Voxels), "hull");
                Assert.AreEqual(20 * 6 * 12, Solid(turretPart.Voxels), "turret");
                Assert.AreEqual(0, Bumps(hullPart.Voxels, hullPart.Size), "hull bumps");
                Assert.AreEqual(0, Bumps(turretPart.Voxels, turretPart.Size), "turret bumps");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private VoxelizerPartGrid Voxelize(bool snap, params Mesh[] meshes)
        {
            var renderers = new List<Renderer>();
            foreach (Mesh m in meshes)
                renderers.Add(TestMeshes.Add(_root.transform, "Part", m, Color.gray));
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, renderers);
            if (snap)
                VoxelizerStraighten.SnapPlanes(triangles, 0.1f);
            VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);
            VoxelizerSurface.Fill(grid);
            return grid;
        }

        /// <summary>The solid cells are exactly one block of this size, its outer layer surface and the rest inside.</summary>
        private static void AssertBlock(VoxelizerPartGrid grid, int3 size, bool checkShell = true)
        {
            int3 lo = new int3(int.MaxValue), hi = new int3(int.MinValue);
            int solid = 0, surface = 0;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                if (grid.Cells[i].a == 0) continue;
                solid++;
                if (grid.Cells[i].a == VoxelizerPartGrid.SURFACE) surface++;
                var p = new int3(i % grid.Size.x, i / grid.Size.x % grid.Size.y, i / (grid.Size.x * grid.Size.y));
                lo = math.min(lo, p);
                hi = math.max(hi, p);
            }

            Assert.AreEqual(size, hi - lo + 1, "bounds");
            Assert.AreEqual(size.x * size.y * size.z, solid, "solid");
            if (!checkShell) return;
            int3 inner = size - 2;
            Assert.AreEqual(size.x * size.y * size.z - inner.x * inner.y * inner.z, surface, "surface shell");
        }

        private static int Solid(Unity.Collections.NativeArray<byte> voxels)
        {
            int solid = 0;
            foreach (byte b in voxels) if (b != 0) solid++;
            return solid;
        }

        /// <summary>Solid cells with exactly one solid face neighbour: a voxel sticking out of a plane.</summary>
        private static int Bumps(Unity.Collections.NativeArray<byte> voxels, int3 size)
        {
            bool Solid(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < size.x && y < size.y && z < size.z && voxels[x + size.x * (y + size.y * z)] != 0;
            int bumps = 0;
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                if (!Solid(x, y, z)) continue;
                int n = (Solid(x + 1, y, z) ? 1 : 0) + (Solid(x - 1, y, z) ? 1 : 0) + (Solid(x, y + 1, z) ? 1 : 0)
                    + (Solid(x, y - 1, z) ? 1 : 0) + (Solid(x, y, z + 1) ? 1 : 0) + (Solid(x, y, z - 1) ? 1 : 0);
                if (n <= 1) bumps++;
            }

            return bumps;
        }
    }
}
