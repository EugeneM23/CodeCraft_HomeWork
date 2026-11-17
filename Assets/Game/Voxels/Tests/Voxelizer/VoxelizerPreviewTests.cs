using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPreviewTests
    {
        [Test]
        public void ACubeOfThree_HasOnlyItsOuterFaces()
        {
            var voxels = new NativeArray<byte>(27, Allocator.Temp);
            for (int i = 0; i < 27; i++) voxels[i] = 1;
            var palette = new Color32[256];
            palette[1] = new Color32(255, 0, 0, 255);
            Mesh mesh = VoxelizerPreview.BuildMesh(new int3(3, 3, 3), voxels, palette, 0.1f, float3.zero);
            Assert.AreEqual(6 * 9 * 2, mesh.triangles.Length / 3);
            Object.DestroyImmediate(mesh);
            voxels.Dispose();
        }

        [Test]
        public void EveryFace_LooksAwayFromItsVoxel()
        {
            var voxels = new NativeArray<byte>(1, Allocator.Temp);
            voxels[0] = 1;
            var palette = new Color32[256];
            palette[1] = new Color32(0, 255, 0, 255);
            Mesh mesh = VoxelizerPreview.BuildMesh(new int3(1, 1, 1), voxels, palette, 1f, new float3(-0.5f, -0.5f, -0.5f));
            Vector3[] v = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] t = mesh.triangles;
            Assert.AreEqual(12, t.Length / 3);
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, mesh.indexFormat);
            for (int i = 0; i < t.Length; i += 3)
            {
                // Unity's (left-handed) front face: Cross(v1 - v0, v2 - v0) is its outward normal.
                Vector3 cross = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Vector3 centre = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(cross, centre), 0f, $"triangle {i / 3} faces inwards");
                Assert.Greater(Vector3.Dot(normals[t[i]], centre), 0f, $"triangle {i / 3} normal points inwards");
            }

            Object.DestroyImmediate(mesh);
            voxels.Dispose();
        }
    }
}
