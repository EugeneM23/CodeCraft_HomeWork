using System.Collections.Generic;
using System.Linq;

namespace Saving
{
    /// <summary>
    /// Slots kept in memory with the same backup rules as the file storage: for tests and for platforms that hand the
    /// text to their own storage API. <see cref="Put"/> and <see cref="Has"/> let tests plant and inspect files.
    /// </summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<int, Dictionary<string, string>> _slots = new();

        public IReadOnlyList<int> ListSlots() =>
            _slots.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).OrderBy(slot => slot).ToList();

        public bool TryRead(int slot, string file, out string text) => TryGet(slot, file, out text);

        public bool TryReadBackup(int slot, string file, out string text) =>
            TryGet(slot, file + SaveFiles.BACKUP_SUFFIX, out text);

        public void Write(int slot, string file, string text)
        {
            Dictionary<string, string> files = FilesOf(slot);
            if (files.TryGetValue(file, out string previous))
                files[file + SaveFiles.BACKUP_SUFFIX] = previous;

            files[file] = text;
        }

        public void Quarantine(int slot, string file, bool backup)
        {
            string name = backup ? file + SaveFiles.BACKUP_SUFFIX : file;
            if (!_slots.TryGetValue(slot, out Dictionary<string, string> files) || !files.TryGetValue(name, out string text))
                return;

            files.Remove(name);
            files[name + SaveFiles.CORRUPT_SUFFIX] = text;
        }

        public void DeleteSlot(int slot) => _slots.Remove(slot);

        /// <summary>Writes a file as is, without a backup: for tests that plant broken or foreign files.</summary>
        public void Put(int slot, string file, string text) => FilesOf(slot)[file] = text;

        public bool Has(int slot, string file) =>
            _slots.TryGetValue(slot, out Dictionary<string, string> files) && files.ContainsKey(file);

        private bool TryGet(int slot, string file, out string text)
        {
            text = null;
            return _slots.TryGetValue(slot, out Dictionary<string, string> files) && files.TryGetValue(file, out text);
        }

        private Dictionary<string, string> FilesOf(int slot)
        {
            if (!_slots.TryGetValue(slot, out Dictionary<string, string> files))
            {
                files = new Dictionary<string, string>();
                _slots.Add(slot, files);
            }

            return files;
        }
    }
}
