using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerSurfaceTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ABoxOfOneMetre_AtTenCentimetres_HasTheShellOfATenCube()
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);

            int surface = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a == 255) surface++;

            Assert.AreEqual(10 * 10 * 10 - 8 * 8 * 8, surface);
        }

        [Test]
        public void ATextureInTwoColours_GivesExactlyThoseTwoColours()
        {
            Texture2D texture = TestMeshes.TwoColours(Color.red, Color.blue);
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.white, texture);
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);

            // Color32 has no value equality: compare colours as rgb ints.
            var colours = new HashSet<int>();
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) colours.Add(c.r << 16 | c.g << 8 | c.b);

            Assert.AreEqual(2, colours.Count);
            Object.DestroyImmediate(texture);
        }
    }
}
