using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Saving
{
    /// <summary>
    /// Slots over any key-value store (a database table, PlayerPrefs, a platform API): a subclass gives three methods
    /// by string key and gets the whole storage contract — backups, quarantine, slot listing, deletion. Keys are
    /// &lt;prefix&gt;slot_N/&lt;file&gt;; &lt;prefix&gt;index lists the files of every slot, because not every store can
    /// list its keys. Each change ends with <see cref="Flush"/>.
    /// </summary>
    public abstract class KeyValueSaveStorage : ISaveStorage
    {
        public const string DEFAULT_PREFIX = "saves/";

        private const string INDEX = "index";
        private const string SLOT_PREFIX = "slot_";

        protected KeyValueSaveStorage(string prefix = DEFAULT_PREFIX) => Prefix = prefix ?? string.Empty;

        public string Prefix { get; }

        /// <summary>The store's key of a slot's file.</summary>
        public string KeyOf(int slot, string file)
        {
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");

            if (string.IsNullOrEmpty(file))
                throw new ArgumentException("The file name must not be empty.", nameof(file));

            return $"{Prefix}{SLOT_PREFIX}{slot.ToString(CultureInfo.InvariantCulture)}/{file}";
        }

        protected abstract bool TryGet(string key, out string text);
        protected abstract void Set(string key, string text);
        protected abstract void Remove(string key);

        /// <summary>Makes the changes durable, such as PlayerPrefs.Save; nothing by default.</summary>
        protected virtual void Flush()
        {
        }

        public IReadOnlyList<int> ListSlots() =>
            ReadIndex().Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).OrderBy(slot => slot).ToList();

        public bool TryRead(int slot, string file, out string text) => TryGet(KeyOf(slot, file), out text);

        public bool TryReadBackup(int slot, string file, out string text) =>
            TryGet(KeyOf(slot, file + SaveFiles.BACKUP_SUFFIX), out text);

        public void Write(int slot, string file, string text)
        {
            SortedDictionary<int, SortedSet<string>> index = ReadIndex();
            string key = KeyOf(slot, file);
            if (TryGet(key, out string previous))
            {
                Set(KeyOf(slot, file + SaveFiles.BACKUP_SUFFIX), previous);
                FilesOf(index, slot).Add(file + SaveFiles.BACKUP_SUFFIX);
            }

            Set(key, text);
            FilesOf(index, slot).Add(file);
            WriteIndex(index);
            Flush();
        }

        public void Quarantine(int slot, string file, bool backup)
        {
            string name = backup ? file + SaveFiles.BACKUP_SUFFIX : file;
            if (!TryGet(KeyOf(slot, name), out string text))
                return;

            SortedDictionary<int, SortedSet<string>> index = ReadIndex();
            Set(KeyOf(slot, name + SaveFiles.CORRUPT_SUFFIX), text);
            Remove(KeyOf(slot, name));
            SortedSet<string> files = FilesOf(index, slot);
            files.Remove(name);
            files.Add(name + SaveFiles.CORRUPT_SUFFIX);
            WriteIndex(index);
            Flush();
        }

        public void DeleteSlot(int slot)
        {
            SortedDictionary<int, SortedSet<string>> index = ReadIndex();
            if (index.TryGetValue(slot, out SortedSet<string> files))
            {
                foreach (string file in files)
                    Remove(KeyOf(slot, file));
            }

            index.Remove(slot);
            WriteIndex(index);
            Flush();
        }

        /// <summary>Removes every slot and the index: nothing of this prefix stays in the store.</summary>
        public void DeleteAll()
        {
            foreach (int slot in ReadIndex().Keys.ToList())
                DeleteSlot(slot);

            Remove(Prefix + INDEX);
            Flush();
        }

        private SortedDictionary<int, SortedSet<string>> ReadIndex()
        {
            var index = new SortedDictionary<int, SortedSet<string>>();
            if (!TryGet(Prefix + INDEX, out string text)
                || !SaveJson.TryRead(text, out Dictionary<string, List<string>> read))
                return index;

            foreach (KeyValuePair<string, List<string>> pair in read)
            {
                if (int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int slot) && pair.Value != null)
                    index[slot] = new SortedSet<string>(pair.Value.Where(file => !string.IsNullOrEmpty(file)), StringComparer.Ordinal);
            }

            return index;
        }

        private void WriteIndex(SortedDictionary<int, SortedSet<string>> index) =>
            Set(Prefix + INDEX, SaveJson.Write(index
                .Where(pair => pair.Value.Count > 0)
                .ToDictionary(pair => pair.Key.ToString(CultureInfo.InvariantCulture), pair => pair.Value.ToList())));

        private static SortedSet<string> FilesOf(SortedDictionary<int, SortedSet<string>> index, int slot)
        {
            if (!index.TryGetValue(slot, out SortedSet<string> files))
            {
                files = new SortedSet<string>(StringComparer.Ordinal);
                index.Add(slot, files);
            }

            return files;
        }
    }
}
