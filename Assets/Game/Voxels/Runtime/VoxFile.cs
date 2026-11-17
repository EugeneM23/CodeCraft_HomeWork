using System;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// MagicaVoxel .vox, one model: SIZE, XYZI and RGBA under MAIN. Its Z is up, ours is Y.
    /// Palette entry i of the file is our colour index i + 1. Alpha (our wetness) is read back as dry.
    /// </summary>
    public static class VoxFile
    {
        public const int MAX_SIZE = 256;

        public static byte[] Write(int3 size, NativeArray<byte> voxels, Color32[] palette)
        {
            if (math.any(size > MAX_SIZE))
                throw new ArgumentException($"A .vox model holds at most {MAX_SIZE} voxels a side; this one is {size}.");

            var xyzi = new MemoryStream();
            var w = new BinaryWriter(xyzi);
            int count = 0;
            w.Write(0);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                byte v = voxels[x + size.x * (y + size.y * z)];
                if (v == 0) continue;
                w.Write((byte)x); w.Write((byte)z); w.Write((byte)y); w.Write(v);
                count++;
            }

            byte[] xyziBytes = xyzi.ToArray();
            BitConverter.GetBytes(count).CopyTo(xyziBytes, 0);

            var rgba = new byte[256 * 4];
            for (int i = 0; i < 255; i++)
            {
                Color32 c = palette[i + 1];
                rgba[i * 4] = c.r; rgba[i * 4 + 1] = c.g; rgba[i * 4 + 2] = c.b; rgba[i * 4 + 3] = 255;
            }

            var sizeBytes = new byte[12];
            BitConverter.GetBytes(size.x).CopyTo(sizeBytes, 0);
            BitConverter.GetBytes(size.z).CopyTo(sizeBytes, 4);
            BitConverter.GetBytes(size.y).CopyTo(sizeBytes, 8);

            var children = new MemoryStream();
            WriteChunk(children, "SIZE", sizeBytes);
            WriteChunk(children, "XYZI", xyziBytes);
            WriteChunk(children, "RGBA", rgba);
            byte[] childBytes = children.ToArray();

            var file = new MemoryStream();
            var f = new BinaryWriter(file);
            f.Write(Encoding.ASCII.GetBytes("VOX "));
            f.Write(150);
            f.Write(Encoding.ASCII.GetBytes("MAIN"));
            f.Write(0);
            f.Write(childBytes.Length);
            f.Write(childBytes);
            return file.ToArray();
        }

        public static bool Read(byte[] file, out int3 size, out byte[] voxels, out Color32[] palette)
        {
            size = int3.zero;
            voxels = null;
            palette = new Color32[256];
            if (file == null || file.Length < 20 || Encoding.ASCII.GetString(file, 0, 4) != "VOX ")
                return false;

            int at = 8;
            int mainContent = BitConverter.ToInt32(file, at + 4);
            at += 12 + mainContent; // MAIN header and its own content
            byte[] xyzi = null;
            bool hasPalette = false;
            while (at + 12 <= file.Length)
            {
                string id = Encoding.ASCII.GetString(file, at, 4);
                int content = BitConverter.ToInt32(file, at + 4);
                int children = BitConverter.ToInt32(file, at + 8);
                int body = at + 12;
                if (content < 0 || children < 0 || body + content > file.Length)
                    return false;
                if (id == "SIZE" && size.x == 0 && content >= 12)
                    size = new int3(BitConverter.ToInt32(file, body), BitConverter.ToInt32(file, body + 8), BitConverter.ToInt32(file, body + 4));
                else if (id == "XYZI" && xyzi == null)
                {
                    xyzi = new byte[content];
                    Array.Copy(file, body, xyzi, 0, content);
                }
                else if (id == "RGBA" && content >= 1020)
                {
                    hasPalette = true;
                    for (int i = 0; i < 255; i++)
                        palette[i + 1] = new Color32(file[body + i * 4], file[body + i * 4 + 1], file[body + i * 4 + 2], 255);
                }

                at = body + content + children;
            }

            if (xyzi == null || xyzi.Length < 4 || math.any(size <= 0) || math.any(size > MAX_SIZE))
                return false;
            if (!hasPalette)
                for (int i = 1; i < 256; i++) palette[i] = new Color32(200, 200, 200, 255);

            voxels = new byte[size.x * size.y * size.z];
            int count = math.min(BitConverter.ToInt32(xyzi, 0), (xyzi.Length - 4) / 4);
            for (int k = 0; k < count; k++)
            {
                int x = xyzi[4 + k * 4], z = xyzi[5 + k * 4], y = xyzi[6 + k * 4];
                if (x < size.x && y < size.y && z < size.z)
                    voxels[x + size.x * (y + size.y * z)] = xyzi[7 + k * 4];
            }

            return true;
        }

        private static void WriteChunk(Stream stream, string id, byte[] content)
        {
            var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes(id));
            w.Write(content.Length);
            w.Write(0);
            w.Write(content);
        }
    }
}
