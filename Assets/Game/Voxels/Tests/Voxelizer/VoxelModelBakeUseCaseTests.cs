using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelBakeUseCaseTests
    {
        [Test]
        public void ToAsset_KeepsEveryPartAndTheClassSettings()
        {
            var result = new VoxelizerResult { VoxelSize = 0.03f, Palette = new Color32[256] };
            var voxels = new NativeArray<byte>(27, Allocator.Persistent);
            for (int i = 0; i < 27; i++) voxels[i] = 1;
            result.Parts.Add(new VoxelizerResultPart { Path = "Hull", Size = new Unity.Mathematics.int3(3, 3, 3), Voxels = voxels });
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;

            VoxelModelAsset asset = VoxelModelBakeUseCase.ToAsset(result, config);

            Assert.AreEqual(1, asset.parts.Length);
            Assert.AreEqual("Hull", asset.parts[0].path);
            Assert.AreEqual(0.45f, asset.toughness);
            Assert.AreEqual(7800f, asset.density);
            Assert.AreEqual(VoxelAnchor.Largest, asset.anchor);
            Assert.IsFalse(asset.colliders);
            NativeArray<byte> back = VoxelModelAsset.Decode(asset.parts[0].runs, 27, Allocator.Temp);
            Assert.AreEqual(27, System.Linq.Enumerable.Count(back, b => b == 1));
            back.Dispose();
            result.Dispose();
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(config);
        }
    }
}
