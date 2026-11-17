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

        private static byte[] Valid()
        {
            var voxels = new NativeArray<byte>(2, Allocator.Temp);
            voxels[0] = 1;
            byte[] file = VoxFile.Write(new int3(2, 1, 1), voxels, new Color32[256]);
            voxels.Dispose();
            return file;
        }

        private static void Patch(byte[] file, int at, int value) => System.BitConverter.GetBytes(value).CopyTo(file, at);

        [Test]
        public void Write_PutsOurYInTheFilesZ()
        {
            var size = new int3(3, 4, 5);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Temp);
            voxels[1 + size.x * (2 + size.y * 0)] = 7; // ours (1, 2, 0)
            byte[] file = VoxFile.Write(size, voxels, new Color32[256]);
            voxels.Dispose();

            // MAIN header 20 bytes, SIZE chunk (12 + 12) at 20, XYZI chunk at 44: count, then x y z i.
            Assert.AreEqual(3, System.BitConverter.ToInt32(file, 32));
            Assert.AreEqual(5, System.BitConverter.ToInt32(file, 36));
            Assert.AreEqual(4, System.BitConverter.ToInt32(file, 40));
            Assert.AreEqual(1, System.BitConverter.ToInt32(file, 56));
            Assert.AreEqual(new byte[] { 1, 0, 2, 7 }, new[] { file[60], file[61], file[62], file[63] });
        }

        [Test]
        public void Read_RefusesBrokenLengthsWithoutThrowing()
        {
            byte[] huge = Valid(); Patch(huge, 48, int.MaxValue);           // XYZI content
            byte[] hugeChildren = Valid(); Patch(hugeChildren, 28, int.MaxValue); // SIZE children
            byte[] hugeMain = Valid(); Patch(hugeMain, 12, int.MaxValue);   // MAIN content
            byte[] negativeMain = Valid(); Patch(negativeMain, 12, int.MinValue);
            byte[] negativeContent = Valid(); Patch(negativeContent, 24, -5);
            byte[] negativeChildren = Valid(); Patch(negativeChildren, 28, -100);
            byte[] valid = Valid();
            var truncated = new byte[valid.Length - 40];
            System.Array.Copy(valid, truncated, truncated.Length);

            foreach (byte[] file in new[] { huge, hugeChildren, hugeMain, negativeMain, negativeContent, negativeChildren, truncated })
                Assert.DoesNotThrow(() => Assert.IsFalse(VoxFile.Read(file, out _, out _, out _)));
        }
    }
}
