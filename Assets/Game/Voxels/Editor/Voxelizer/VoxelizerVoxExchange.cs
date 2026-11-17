using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Moves one part of a baked VoxelModelAsset to and from a MagicaVoxel .vox file.</summary>
    public static class VoxelizerVoxExchange
    {
        public static byte[] Export(VoxelModelAsset asset, int partIndex)
        {
            VoxelModelAsset.Part part = asset.parts[partIndex];
            var size = new int3(part.size.x, part.size.y, part.size.z);
            NativeArray<byte> voxels = VoxelModelAsset.Decode(part.runs, size.x * size.y * size.z, Allocator.Temp);
            try { return VoxFile.Write(size, voxels, asset.palette); }
            finally { voxels.Dispose(); }
        }

        /// <summary>
        /// Replaces the part's grid and size with the file's (origin stays). A file colour takes the palette
        /// slot of the same colour; a new colour takes a slot no other part uses; with none left, the nearest colour.
        /// </summary>
        public static void Import(VoxelModelAsset asset, int partIndex, byte[] file)
        {
            if (!VoxFile.Read(file, out int3 size, out byte[] voxels, out Color32[] filePalette))
                throw new ArgumentException("Not a readable .vox file.");

            Color32[] palette = asset.palette;
            var usedElsewhere = new bool[256];
            for (int p = 0; p < asset.parts.Length; p++)
            {
                if (p == partIndex) continue;
                NativeArray<byte> other = VoxelModelAsset.Decode(asset.parts[p].runs,
                    asset.parts[p].size.x * asset.parts[p].size.y * asset.parts[p].size.z, Allocator.Temp);
                foreach (byte v in other) usedElsewhere[v] = true;
                other.Dispose();
            }

            var usedInFile = new bool[256];
            foreach (byte v in voxels) usedInFile[v] = true;

            var map = new byte[256];
            var claimed = new bool[256];
            // Same colour first, so a round trip leaves the palette alone.
            for (int i = 1; i < 256; i++)
            {
                if (!usedInFile[i]) continue;
                for (int s = 1; s < 256; s++)
                    if (palette[s].a > 0 && Same(palette[s], filePalette[i]))
                    {
                        map[i] = (byte)s;
                        claimed[s] = true;
                        break;
                    }
            }

            for (int i = 1; i < 256; i++)
            {
                if (!usedInFile[i] || map[i] != 0) continue;
                int slot = FreeSlot(palette, usedElsewhere, claimed);
                if (slot == 0)
                    slot = Nearest(palette, filePalette[i], usedElsewhere, claimed);
                else
                    palette[slot] = new Color32(filePalette[i].r, filePalette[i].g, filePalette[i].b, 255);
                map[i] = (byte)slot;
                claimed[slot] = true;
            }

            var mapped = new NativeArray<byte>(voxels.Length, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) mapped[i] = map[voxels[i]];
            VoxelModelAsset.Part part = asset.parts[partIndex];
            part.size = new Vector3Int(size.x, size.y, size.z);
            part.runs = VoxelModelAsset.Encode(mapped);
            mapped.Dispose();
            EditorUtility.SetDirty(asset);
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        /// <summary>The lowest slot no other part uses and nothing in this import has claimed, or 0.</summary>
        private static int FreeSlot(Color32[] palette, bool[] usedElsewhere, bool[] claimed)
        {
            for (int s = 1; s < 256; s++)
                if (!usedElsewhere[s] && !claimed[s]) return s;
            return 0;
        }

        private static int Nearest(Color32[] palette, Color32 colour, bool[] usedElsewhere, bool[] claimed)
        {
            int best = 1, bestDistance = int.MaxValue;
            for (int s = 1; s < 256; s++)
            {
                if (!usedElsewhere[s] && !claimed[s]) continue;
                int dr = palette[s].r - colour.r, dg = palette[s].g - colour.g, db = palette[s].b - colour.b;
                int d = dr * dr + dg * dg + db * db;
                if (d < bestDistance) { bestDistance = d; best = s; }
            }

            return best;
        }
    }
}
