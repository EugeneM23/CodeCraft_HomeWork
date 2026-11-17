using Game.Voxels;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxFileTests
    {
        [Test]
        public void WriteThenRead_GivesTheSameGridAndColours()
        {
            int3 size = new int3(5, 7, 3);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) voxels[i] = (byte)(i % 4);
            var palette = new Color32[256];
            for (int i = 1; i < 256; i++) palette[i] = new Color32((byte)i, (byte)(255 - i), 7, 255);

            byte[] file = VoxFile.Write(size, voxels, palette);
            Assert.IsTrue(VoxFile.Read(file, out int3 backSize, out byte[] back, out Color32[] backPalette));

            Assert.AreEqual(size, backSize);
            Assert.AreEqual(voxels.ToArray(), back);
            for (int i = 1; i < 256; i++)
                Assert.AreEqual(palette[i].r, backPalette[i].r);
            voxels.Dispose();
        }

        [Test]
        public void Write_RefusesAPartOverTwoHundredFiftySixVoxels()
        {
            var voxels = new NativeArray<byte>(300, Allocator.Temp);
            Assert.Throws<System.ArgumentException>(() => VoxFile.Write(new int3(300, 1, 1), voxels, new Color32[256]));
            voxels.Dispose();
        }

        [Test]
        public void Read_RefusesGarbage()
        {
            Assert.IsFalse(VoxFile.Read(new byte[] { 1, 2, 3 }, out _, out _, out _));
            Assert.IsFalse(VoxFile.Read(new byte[40], out _, out _, out _));
        }
    }
}
