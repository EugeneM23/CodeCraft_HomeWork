using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPreviewTests
    {
        private const string FOLDER = "Assets/__VoxelizerPreviewTest";

        [Test]
        public void AFailedRecompute_LeavesNoPanelsOrSummary()
        {
            AssetDatabase.DeleteAsset(FOLDER);
            AssetDatabase.CreateFolder("Assets", "__VoxelizerPreviewTest");
            var preview = new VoxelizerPreview();
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            try
            {
                var box = new GameObject("Box");
                MeshRenderer r = TestMeshes.Add(box.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
                AssetDatabase.CreateAsset(r.GetComponent<MeshFilter>().sharedMesh, FOLDER + "/Box.asset");
                AssetDatabase.CreateAsset(r.sharedMaterial, FOLDER + "/Box.mat");
                GameObject small = PrefabUtility.SaveAsPrefabAsset(box, FOLDER + "/Small.prefab");
                box.transform.localScale = Vector3.one * 100f; // 100 m: over 512 cells, Run throws
                GameObject huge = PrefabUtility.SaveAsPrefabAsset(box, FOLDER + "/Huge.prefab");
                Object.DestroyImmediate(box);
                config.kind = VoxelizerConfig.ModelKind.Vehicle;

                config.model = small;
                preview.Recompute(config);
                Assert.IsTrue(preview.HasModel);
                config.model = huge;
                Assert.Catch(() => preview.Recompute(config));

                Assert.IsFalse(preview.HasModel, "no stale panels");
                Assert.AreEqual("", preview.Summary);
                Assert.AreEqual("", preview.PartCounts);
            }
            finally
            {
                preview.Dispose();
                Object.DestroyImmediate(config);
                AssetDatabase.DeleteAsset(FOLDER);
            }
        }

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
