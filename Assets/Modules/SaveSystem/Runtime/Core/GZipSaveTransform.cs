using System.IO;
using System.IO.Compression;

namespace Saving
{
    /// <summary>Compresses a slot's text with GZip: JSON shrinks several times.</summary>
    public sealed class GZipSaveTransform : ISaveTransform
    {
        public string Id => "gzip";

        public byte[] Encode(byte[] data)
        {
            using var output = new MemoryStream();
            using (var zip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                zip.Write(data, 0, data.Length);

            return output.ToArray();
        }

        public byte[] Decode(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var zip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zip.CopyTo(output);
            return output.ToArray();
        }
    }
}
