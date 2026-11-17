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
            // The grid starts at −0.1: x and z 0.42..0.48 lie in cell 5 only, y 1.001..1.5 in cells 11..15 (its top lies on a boundary and stays in the cell below it).
            Assert.AreEqual(1000, SolidAfterOpen(new Vector3(0.42f, 1.001f, 0.42f), new Vector3(0.48f, 1.5f, 0.48f)));
        }

        [Test]
        public void ARodTwoVoxelsThick_Stays()
        {
            // x and z 0.42..0.58: cells 5 and 6; y cells 11..15: 2 × 2 × 5 = 20 on top of the box.
            Assert.AreEqual(1020, SolidAfterOpen(new Vector3(0.42f, 1.001f, 0.42f), new Vector3(0.58f, 1.5f, 0.58f)));
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

        [Test]
        public void AFaceWhoseLowCornerIsTwoLayersBelowItsMean_IsNotSnapped_TheBodyStaysSolid()
        {
            // A prism 1 m deep: flat top at 0.295 (x 0.05..2.05) and a 9.3° slope down to 0.099 at x 3.25,
            // a steep side from there to the floor. Top and slope are one +Y face spreading 0.196 m (under two
            // voxels), its mean 0.258 in layer 2, its low corner in layer 0. Snapped to layer 2, the side
            // would end in layer 0 and leave layer 1 open: the outside fill would run into the body.
            Vector2[] profile = { new(0.05f, 0.05f), new(3.25f, 0.05f), new(3.25f, 0.099f), new(2.05f, 0.295f), new(0.05f, 0.295f) };
            MeshRenderer r = TestMeshes.Add(_root.transform, "Prism", Prism(profile, new[] { profile }, 0.05f, 1.05f), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);

            // Layer 1 (y 0.1..0.2) is inside the body all along it.
            for (float x = 0.15f; x < 3.2f; x += 0.1f)
                Assert.AreNotEqual(0, CellAt(grid, new Vector3(x, 0.15f, 0.55f)).a, $"x {x:0.00}");
        }

        [Test]
        public void TwoTopsJoinedByARiser_EachLandsFlatInItsOwnLayer()
        {
            // An L-shaped step, 1 m deep: the left top at 0.52 (layer 5), the right at 0.68 (layer 6), one
            // welded mesh. The riser between them is ±X, so the tops must not join into one face.
            Vector2[] profile = { new(0.05f, 0.05f), new(2.05f, 0.05f), new(2.05f, 0.68f), new(1.05f, 0.68f), new(1.05f, 0.52f), new(0.05f, 0.52f) };
            Vector2[][] caps =
            {
                new[] { new Vector2(0.05f, 0.05f), new Vector2(1.05f, 0.05f), new Vector2(1.05f, 0.52f), new Vector2(0.05f, 0.52f) },
                new[] { new Vector2(1.05f, 0.05f), new Vector2(2.05f, 0.05f), new Vector2(2.05f, 0.68f), new Vector2(1.05f, 0.68f) }
            };
            MeshRenderer r = TestMeshes.Add(_root.transform, "Step", Prism(profile, caps, 0.05f, 1.05f), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);

            for (float z = 0.15f; z < 1f; z += 0.1f)
            {
                for (float x = 0.15f; x < 1f; x += 0.1f)
                    Assert.AreEqual(0.55f, TopAt(grid, x, z), 1e-4f, $"left {x:0.00},{z:0.00}");
                for (float x = 1.15f; x < 2f; x += 0.1f)
                    Assert.AreEqual(0.65f, TopAt(grid, x, z), 1e-4f, $"right {x:0.00},{z:0.00}");
            }
        }

        /// <summary>The profile (counter-clockwise in XY) extruded from z0 to z1, closed by convex cap pieces, outward winding.</summary>
        private static Mesh Prism(Vector2[] profile, Vector2[][] caps, float z0, float z1)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                int i = vertices.Count;
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
                vertices.Add(a);
                vertices.Add(flip ? c : b);
                vertices.Add(flip ? b : c);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }

            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 p = profile[i], q = profile[(i + 1) % profile.Length];
                var outward = new Vector3(q.y - p.y, p.x - q.x, 0f);
                Vector3 a = new(p.x, p.y, z0), b = new(q.x, q.y, z0), c = new(q.x, q.y, z1), d = new(p.x, p.y, z1);
                Triangle(a, b, c, outward);
                Triangle(a, c, d, outward);
            }

            foreach (Vector2[] cap in caps)
                for (int i = 1; i + 1 < cap.Length; i++)
                {
                    Triangle(new Vector3(cap[0].x, cap[0].y, z0), new Vector3(cap[i].x, cap[i].y, z0), new Vector3(cap[i + 1].x, cap[i + 1].y, z0), Vector3.back);
                    Triangle(new Vector3(cap[0].x, cap[0].y, z1), new Vector3(cap[i].x, cap[i].y, z1), new Vector3(cap[i + 1].x, cap[i + 1].y, z1), Vector3.forward);
                }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Color32 CellAt(VoxelizerPartGrid grid, Vector3 p)
        {
            Vector3 local = (p - (Vector3)grid.Origin) / grid.VoxelSize;
            int x = Mathf.FloorToInt(local.x), y = Mathf.FloorToInt(local.y), z = Mathf.FloorToInt(local.z);
            return grid.Cells[x + grid.Size.x * (y + grid.Size.y * z)];
        }

        /// <summary>World height of the centre of the highest solid cell in the column through (x, z).</summary>
        private static float TopAt(VoxelizerPartGrid grid, float x, float z)
        {
            for (int y = grid.Size.y - 1; y >= 0; y--)
            {
                float h = grid.Origin.y + (y + 0.5f) * grid.VoxelSize;
                if (CellAt(grid, new Vector3(x, h, z)).a != 0)
                    return h;
            }

            return float.NaN;
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
