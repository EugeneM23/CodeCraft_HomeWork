using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPaletteTests
    {
        private static VoxelizerPartGrid Noisy(int colours, int3 size)
        {
            var g = new VoxelizerPartGrid { Size = size, VoxelSize = 0.1f, Cells = new NativeArray<Color32>(size.x * size.y * size.z, Allocator.Persistent) };
            var random = new Unity.Mathematics.Random(7);
            for (int i = 0; i < g.Cells.Length; i++)
            {
                int c = random.NextInt(colours);
                g.Cells[i] = new Color32((byte)(c * 255 / colours), (byte)(255 - c * 255 / colours), 90, VoxelizerPartGrid.SURFACE);
            }
            return g;
        }

        [Test]
        public void ThePaletteHoldsAtMostKSurfaceAndKInsideColours()
        {
            using VoxelizerPartGrid g = Noisy(40, new int3(16, 16, 16));
            Color32[] palette = VoxelizerPalette.Build(new List<VoxelizerPartGrid> { g }, 12, out _);
            int used = 0;
            for (int i = 1; i < 256; i++) if (palette[i].a != 0) used++;
            Assert.LessOrEqual(used, 24);
            Assert.AreEqual(0, palette[0].a);
        }

        [Test]
        public void TwoColours_StayTwo()
        {
            var g = new VoxelizerPartGrid { Size = new int3(4, 4, 4), VoxelSize = 0.1f, Cells = new NativeArray<Color32>(64, Allocator.Persistent) };
            for (int i = 0; i < 64; i++)
                g.Cells[i] = i < 32 ? new Color32(200, 30, 30, 255) : new Color32(30, 30, 200, 255);
            Build2(g, out NativeArray<byte> voxels);
            var used = new HashSet<byte>();
            foreach (byte b in voxels) used.Add(b);
            Assert.AreEqual(2, used.Count);
            voxels.Dispose();
            g.Dispose();
        }

        private static void Build2(VoxelizerPartGrid g, out NativeArray<byte> voxels)
        {
            VoxelizerPalette.Build(new List<VoxelizerPartGrid> { g }, 12, out Color32[] centres);
            voxels = VoxelizerPalette.Map(g, centres, 12, Allocator.Persistent);
        }

        [Test]
        public void ALoneSpeck_TakesItsNeighboursColour()
        {
            int3 size = new int3(5, 5, 5);
            var voxels = new NativeArray<byte>(125, Allocator.Persistent);
            for (int i = 0; i < 125; i++) voxels[i] = 1;
            voxels[2 + 5 * (2 + 5 * 2)] = 2;
            VoxelizerPalette.Smooth(voxels, size, 12);
            Assert.AreEqual(1, voxels[2 + 5 * (2 + 5 * 2)]);
            voxels.Dispose();
        }
    }
}
