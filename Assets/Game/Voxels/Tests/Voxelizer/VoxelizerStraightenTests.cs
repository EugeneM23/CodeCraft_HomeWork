using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerStraightenTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ATopFaceTiltedFiveDegrees_ComesOutFlat()
        {
            // A 2 m × 2 m slab whose top rises 5° along X: 0.5 m high at x 0, ~0.67 m at x 2.
            Mesh mesh = TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.5f, 1.999f));
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                if (v[i].y > 0.25f)
                    v[i].y += v[i].x * Mathf.Tan(5f * Mathf.Deg2Rad);
            mesh.vertices = v;
            MeshRenderer r = TestMeshes.Add(_root.transform, "Slab", mesh, Color.gray);

            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            // The side faces still rise to the old sloped edge: a one-voxel sliver above the snapped
            // top along the high end. «Auto» always opens after filling, which cuts it.
            VoxelizerStraighten.Open(grid, 2);

            // Every column has the same height: the top is one flat layer.
            int expected = -1;
            for (int z = 1; z < grid.Size.z - 1; z++)
            for (int x = 1; x < grid.Size.x - 1; x++)
            {
                int top = -1;
                for (int y = 0; y < grid.Size.y; y++)
                    if (grid.Cells[x + grid.Size.x * (y + grid.Size.y * z)].a != 0) top = y;
                if (top < 0) continue;
                if (expected < 0) expected = top;
                Assert.AreEqual(expected, top, $"column {x},{z}");
            }
        }

        [Test]
        public void ARodOneVoxelThick_IsCutAway_TheBoxStays()
        {
            // The grid starts at −0.1: x and z 0.42..0.48 lie in cell 5 only, y 1.001..1.5 in cells 11..16.
            Assert.AreEqual(1000, SolidAfterOpen(new Vector3(0.42f, 1.001f, 0.42f), new Vector3(0.48f, 1.5f, 0.48f)));
        }

        [Test]
        public void ARodTwoVoxelsThick_Stays()
        {
            // x and z 0.42..0.58: cells 5 and 6; y cells 11..16: 2 × 2 × 6 = 24 on top of the box.
            Assert.AreEqual(1024, SolidAfterOpen(new Vector3(0.42f, 1.001f, 0.42f), new Vector3(0.58f, 1.5f, 0.58f)));
        }

        [Test]
        public void APlateTwoVoxelsThick_KeepsItsThickness()
        {
            // 1.9 m × 0.9 m × 0.19 m, faces off the voxel boundaries: x in cells 1..20, z in 1..10, y 0.205..0.395 in 2 and 3.
            MeshRenderer r = TestMeshes.Add(_root.transform, "Plate",
                TestMeshes.Box(new Vector3(0.05f, 0.205f, 0.05f), new Vector3(1.95f, 0.395f, 0.95f)), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            VoxelizerStraighten.Open(grid, 2);

            int columns = 0;
            for (int z = 0; z < grid.Size.z; z++)
            for (int x = 0; x < grid.Size.x; x++)
            {
                int solid = 0;
                for (int y = 0; y < grid.Size.y; y++)
                    if (grid.Cells[x + grid.Size.x * (y + grid.Size.y * z)].a != 0) solid++;
                if (solid == 0) continue;
                columns++;
                Assert.AreEqual(2, solid, $"column {x},{z}");
            }

            Assert.AreEqual(20 * 10, columns);
        }

        [Test]
        public void ASlopeRisingMoreThanTwoVoxels_IsNotFlattened()
        {
            // A 4 m slab whose top rises 5° along X: 0.35 m, 3.5 voxels. Within the angle, but a ramp, not a flat face.
            Mesh mesh = TestMeshes.Box(Vector3.one * 0.001f, new Vector3(3.999f, 0.5f, 1.999f));
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                if (v[i].y > 0.25f)
                    v[i].y += v[i].x * Mathf.Tan(5f * Mathf.Deg2Rad);
            mesh.vertices = v;
            MeshRenderer r = TestMeshes.Add(_root.transform, "Ramp", mesh, Color.gray);

            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);

            var tops = new HashSet<int>();
            int z0 = grid.Size.z / 2;
            for (int x = 1; x < grid.Size.x - 1; x++)
                for (int y = grid.Size.y - 1; y >= 0; y--)
                    if (grid.Cells[x + grid.Size.x * (y + grid.Size.y * z0)].a != 0)
                    {
                        tops.Add(y);
                        break;
                    }

            Assert.GreaterOrEqual(tops.Count, 4);
        }

        private int SolidAfterOpen(Vector3 rodMin, Vector3 rodMax)
        {
            MeshRenderer box = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
            MeshRenderer rod = TestMeshes.Add(_root.transform, "Rod", TestMeshes.Box(rodMin, rodMax), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { box, rod });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            VoxelizerStraighten.Open(grid, 2);

            int solid = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) solid++;
            return solid;
        }
    }
}
