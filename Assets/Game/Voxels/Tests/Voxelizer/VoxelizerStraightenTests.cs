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
        public void ARodThinnerThanTwoVoxels_IsCutAway_TheBoxStays()
        {
            MeshRenderer box = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
            MeshRenderer rod = TestMeshes.Add(_root.transform, "Rod", TestMeshes.Box(new Vector3(0.451f, 1.001f, 0.451f), new Vector3(0.549f, 1.5f, 0.549f)), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { box, rod });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            VoxelizerStraighten.Open(grid, 2);

            int solid = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) solid++;
            Assert.AreEqual(1000, solid);
        }
    }
}
