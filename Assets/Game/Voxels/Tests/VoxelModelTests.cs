using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelTests
    {
        [UnityTest]
        public IEnumerator ABakedModel_BuildsABodyPerPart_WithEveryVoxel()
        {
            var data = ScriptableObject.CreateInstance<VoxelModelAsset>();
            data.voxelSize = 0.1f;
            data.palette = new Color32[256];
            data.palette[1] = new Color32(200, 200, 200, 255);
            var voxels = new NativeArray<byte>(4 * 4 * 4, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) voxels[i] = 1;
            data.parts = new[] { new VoxelModelAsset.Part { path = "", voxelSize = 0.1f, size = new Vector3Int(4, 4, 4), runs = VoxelModelAsset.Encode(voxels) } };
            voxels.Dispose();

            var go = new GameObject("Model");
            go.SetActive(false);
            VoxelModel model = go.AddComponent<VoxelModel>();
            model.SetData(data);
            go.SetActive(true);
            yield return null;

            Assert.AreEqual(1, model.Bodies.Count);
            Assert.AreEqual(64, model.Bodies[0].SolidCount);
            Object.Destroy(go);
            Object.Destroy(data);
        }
    }
}
