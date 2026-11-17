using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerVoxExchangeTests
    {
        private static VoxelModelAsset Asset(int3 size, out byte[] voxels)
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            asset.palette[1] = new Color32(200, 10, 10, 255);
            asset.palette[2] = new Color32(10, 200, 10, 255);
            voxels = new byte[size.x * size.y * size.z];
            for (int i = 0; i < voxels.Length; i++) voxels[i] = (byte)(i % 3);
            var native = new NativeArray<byte>(voxels, Allocator.Temp);
            asset.parts = new[]
            {
                new VoxelModelAsset.Part { size = new Vector3Int(size.x, size.y, size.z), origin = new Vector3(1, 2, 3), runs = VoxelModelAsset.Encode(native) }
            };
            native.Dispose();
            return asset;
        }

        [Test]
        public void ExportThenImport_LeavesThePartAndPaletteUnchanged()
        {
            VoxelModelAsset asset = Asset(new int3(4, 3, 5), out byte[] voxels);
            Color32[] before = (Color32[])asset.palette.Clone();
            byte[] file = VoxelizerVoxExchange.Export(asset, 0);

            VoxelizerVoxExchange.Import(asset, 0, file);

            NativeArray<byte> back = VoxelModelAsset.Decode(asset.parts[0].runs, voxels.Length, Allocator.Temp);
            Assert.AreEqual(voxels, back.ToArray());
            Assert.AreEqual(new Vector3(1, 2, 3), asset.parts[0].origin);
            Assert.AreEqual(before, asset.palette);
            back.Dispose();
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Import_PutsANewColourInAFreeSlot_AndTakesANewSize()
        {
            VoxelModelAsset asset = Asset(new int3(2, 2, 2), out _);
            var grid = new NativeArray<byte>(3, Allocator.Temp);
            grid[0] = 9; grid[1] = 9; grid[2] = 0;
            var filePalette = new Color32[256];
            filePalette[9] = new Color32(1, 2, 250, 255);
            byte[] file = VoxFile.Write(new int3(3, 1, 1), grid, filePalette);
            grid.Dispose();

            VoxelizerVoxExchange.Import(asset, 0, file);

            Assert.AreEqual(new Vector3Int(3, 1, 1), asset.parts[0].size);
            NativeArray<byte> back = VoxelModelAsset.Decode(asset.parts[0].runs, 3, Allocator.Temp);
            Assert.AreNotEqual(0, back[0]);
            Assert.AreEqual(back[0], back[1]);
            Assert.AreEqual(0, back[2]);
            Assert.AreEqual(250, asset.palette[back[0]].b);
            back.Dispose();
            Object.DestroyImmediate(asset);
        }
    }
}
