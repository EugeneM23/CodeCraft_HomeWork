using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerFillTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private int Solid(params Mesh[] meshes)
        {
            var renderers = new List<Renderer>();
            foreach (Mesh m in meshes)
                renderers.Add(TestMeshes.Add(_root.transform, "Part", m, Color.gray));
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, renderers);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);
            VoxelizerSurface.Fill(grid);
            int solid = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) solid++;
            return solid;
        }

        [Test]
        public void AClosedBox_IsSolid() =>
            Assert.AreEqual(1000, Solid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f)));

        [Test]
        public void TwoOverlappingBoxes_FillAsTheirUnion_WithoutParityStripes()
        {
            // 10×10×10 and a 10×10×10 shifted by 5 along X: union 15×10×10.
            int solid = Solid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f),
                TestMeshes.Box(new Vector3(0.501f, 0.001f, 0.001f), new Vector3(1.499f, 0.999f, 0.999f)));
            Assert.AreEqual(1500, solid);
        }

        [Test]
        public void InsideIsDarkerThanTheSurface()
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), new Color(0.8f, 0.8f, 0.8f));
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);
            VoxelizerSurface.Fill(grid);
            Color32 centre = grid.Cells[5 + grid.Size.x * (5 + grid.Size.y * 5)];
            Assert.AreEqual(VoxelizerPartGrid.INSIDE, centre.a);
            Assert.Less(centre.r, 204);
        }
    }
}
