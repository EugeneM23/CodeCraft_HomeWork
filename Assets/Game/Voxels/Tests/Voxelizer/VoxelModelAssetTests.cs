using Game.Voxels;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelAssetTests
    {
        [Test]
        public void Encode_ThenDecode_GivesTheSameVoxels()
        {
            var voxels = new NativeArray<byte>(1000, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = (byte)(i % 7 == 0 ? 0 : (i / 100) + 1);

            byte[] runs = VoxelModelAsset.Encode(voxels);
            NativeArray<byte> back = VoxelModelAsset.Decode(runs, voxels.Length, Allocator.Temp);

            Assert.AreEqual(voxels.ToArray(), back.ToArray());
            voxels.Dispose();
            back.Dispose();
        }

        [Test]
        public void Encode_AnEmptyGrid_IsTiny()
        {
            // A run is at most 255 voxels, so 262144 zeros take ceil(262144 / 255) = 1029 pairs = 2058 bytes.
            var voxels = new NativeArray<byte>(512 * 512, Allocator.Temp);
            Assert.AreEqual(2058, VoxelModelAsset.Encode(voxels).Length);
            voxels.Dispose();
        }

        [Test]
        public void Settings_CarryTheClassAndThePartOrigin()
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            asset.voxelSize = 0.03f;
            asset.toughness = 0.45f;
            asset.density = 7800f;
            asset.anchor = VoxelAnchor.Largest;
            asset.colliders = false;
            // A part scaled twice in the prefab: its voxel in its own space is half the class voxel.
            var part = new VoxelModelAsset.Part { origin = new Vector3(1f, 2f, 3f), voxelSize = 0.015f };

            VoxelBodySettings s = asset.Settings(part, 5);

            Assert.AreEqual(0.015f, s.VoxelSize);
            Assert.AreEqual(0.45f, s.Toughness);
            Assert.AreEqual(7800f, s.Density);
            Assert.AreEqual(VoxelAnchor.Largest, s.Anchor);
            Assert.IsFalse(s.Colliders);
            Assert.AreEqual(5, s.Layer);
            Assert.AreEqual(2f, s.Origin.y);
            Object.DestroyImmediate(asset);
        }
    }
}
