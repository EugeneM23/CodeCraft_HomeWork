using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Saving
{
    /// <summary>
    /// A "server" that is a folder: slot_N.json packages in a shared or synced folder (a network drive, Dropbox), and
    /// the simplest example of IRemoteSaveStorage to copy for a real backend.
    /// </summary>
    public sealed class FolderRemoteSaveStorage : IRemoteSaveStorage
    {
        private const string PREFIX = "slot_";
        private const string EXTENSION = ".json";

        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public FolderRemoteSaveStorage(string root)
        {
            if (string.IsNullOrEmpty(root))
                throw new System.ArgumentException("The folder must not be empty.", nameof(root));

            Root = root;
        }

        public string Root { get; }

        public Task<IReadOnlyList<int>> ListAsync(CancellationToken token = default)
        {
            var slots = new List<int>();
            if (Directory.Exists(Root))
            {
                foreach (string path in Directory.GetFiles(Root, PREFIX + "*" + EXTENSION))
                {
                    string name = Path.GetFileNameWithoutExtension(path).Substring(PREFIX.Length);
                    if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int slot))
                        slots.Add(slot);
                }
            }

            slots.Sort();
            return Task.FromResult<IReadOnlyList<int>>(slots);
        }

        public Task<string> DownloadAsync(int slot, CancellationToken token = default)
        {
            string path = PathOf(slot);
            return Task.FromResult(File.Exists(path) ? File.ReadAllText(path, Utf8) : null);
        }

        public Task UploadAsync(int slot, string package, CancellationToken token = default)
        {
            Directory.CreateDirectory(Root);
            string path = PathOf(slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, package, Utf8);
            if (File.Exists(path))
                File.Delete(path);

            File.Move(temp, path);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int slot, CancellationToken token = default)
        {
            string path = PathOf(slot);
            if (File.Exists(path))
                File.Delete(path);

            return Task.CompletedTask;
        }

        private string PathOf(int slot) =>
            Path.Combine(Root, PREFIX + slot.ToString(CultureInfo.InvariantCulture) + EXTENSION);
    }
}
