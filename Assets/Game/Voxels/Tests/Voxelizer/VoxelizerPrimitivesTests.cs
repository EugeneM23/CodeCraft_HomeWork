using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPrimitivesTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private VoxelizerPartGrid Grid(Mesh mesh)
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Part", mesh, Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            return VoxelizerSurface.Sample(t, 0.1f);
        }

        private static int Solid(VoxelizerPartGrid g)
        {
            int n = 0;
            foreach (Color32 c in g.Cells) if (c.a != 0) n++;
            return n;
        }

        [Test]
        public void Box_FillsTheBoundsOfARaggedPart()
        {
            Mesh rough = TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.499f, 0.799f));
            // Pull one corner of the top face (face 5 in TestMeshes.Box: vertices 20..23) down to
            // 0.2 m: the top is no longer flat, the bounds stay 1 × 0.5 × 0.8 m.
            Vector3[] v = rough.vertices;
            v[22] = new Vector3(v[22].x, 0.2f, v[22].z);
            rough.vertices = v;
            using VoxelizerPartGrid g = Grid(rough);
            VoxelizerPrimitives.Box(g);
            Assert.AreEqual(10 * 5 * 8, Solid(g));
        }

        [Test]
        public void Cylinder_AlongX_IsRoundAndSymmetric()
        {
            // A 1 m cube marked «cylinder along X»: diameter 10 voxels, length 10.
            using VoxelizerPartGrid g = Grid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f));
            VoxelizerPrimitives.Cylinder(g, 0);
            int solid = Solid(g);
            Assert.AreEqual(10 * 80, solid); // a disc of 10 voxels across holds 80 cells (centres inside r = 5)

            // Mirror symmetry in Y and Z about the middle of the occupied bounds (the grid has a margin around the part).
            int3 lo = g.Size, hi = -1;
            for (int i = 0; i < g.Cells.Length; i++)
            {
                if (g.Cells[i].a == 0) continue;
                int3 p = new int3(i % g.Size.x, (i / g.Size.x) % g.Size.y, i / (g.Size.x * g.Size.y));
                lo = math.min(lo, p);
                hi = math.max(hi, p);
            }

            int mid = (lo.x + hi.x) / 2;
            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            {
                bool a = g.Cells[mid + g.Size.x * (y + g.Size.y * z)].a != 0;
                bool b = g.Cells[mid + g.Size.x * ((lo.y + hi.y - y) + g.Size.y * z)].a != 0;
                bool c = g.Cells[mid + g.Size.x * (y + g.Size.y * (lo.z + hi.z - z))].a != 0;
                Assert.AreEqual(a, b);
                Assert.AreEqual(a, c);
            }
        }

        [Test]
        public void Cylinder_KeepsBothColoursOfATwoColourDisc()
        {
            // A wheel-like part: a red lower half and a blue upper half, each its own renderer.
            var red = new Color(0.9f, 0.1f, 0.1f);
            var blue = new Color(0.1f, 0.1f, 0.9f);
            MeshRenderer a = TestMeshes.Add(_root.transform, "Low", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.499f, 0.999f)), red);
            MeshRenderer b = TestMeshes.Add(_root.transform, "High", TestMeshes.Box(new Vector3(0.001f, 0.501f, 0.001f), Vector3.one * 0.999f), blue);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { a, b });
            using VoxelizerPartGrid g = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerPrimitives.Cylinder(g, 0);

            int3 lo = g.Size;
            for (int i = 0; i < g.Cells.Length; i++)
                if (g.Cells[i].a != 0)
                    lo = math.min(lo, new int3(i % g.Size.x, (i / g.Size.x) % g.Size.y, i / (g.Size.x * g.Size.y)));

            int redCells = 0, blueCells = 0;
            for (int z = 0; z < g.Size.z; z++)
            for (int y = 0; y < g.Size.y; y++)
            {
                Color32 c = g.Cells[lo.x + g.Size.x * (y + g.Size.y * z)];
                if (c.a != VoxelizerPartGrid.SURFACE) continue;
                if (c.r > c.b) redCells++;
                else if (c.b > c.r) blueCells++;
            }

            Assert.Greater(redCells, 0, "the end face lost the red colour");
            Assert.Greater(blueCells, 0, "the end face lost the blue colour");
        }
    }
}
