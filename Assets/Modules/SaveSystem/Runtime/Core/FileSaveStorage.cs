using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Saving
{
    /// <summary>
    /// Slots as folders root/slot_N. A write goes to &lt;file&gt;.tmp in the same folder, is flushed to disk and then
    /// replaces the file, which becomes &lt;file&gt;.bak: a crash mid-write leaves the old save whole.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string SLOT_PREFIX = "slot_";
        private const string TEMP_SUFFIX = ".tmp";

        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public FileSaveStorage(string root)
        {
            if (string.IsNullOrEmpty(root))
                throw new ArgumentException("The root folder must not be empty.", nameof(root));

            Root = root;
        }

        public string Root { get; }

        public IReadOnlyList<int> ListSlots()
        {
            var slots = new List<int>();
            if (!Directory.Exists(Root))
                return slots;

            foreach (string directory in Directory.GetDirectories(Root, SLOT_PREFIX + "*"))
            {
                string number = Path.GetFileName(directory).Substring(SLOT_PREFIX.Length);
                if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int slot)
                    && Directory.GetFiles(directory).Length > 0)
                    slots.Add(slot);
            }

            slots.Sort();
            return slots;
        }

        public bool TryRead(int slot, string file, out string text) => TryReadPath(PathOf(slot, file), out text);

        public bool TryReadBackup(int slot, string file, out string text) =>
            TryReadPath(PathOf(slot, file) + SaveFiles.BACKUP_SUFFIX, out text);

        public void Write(int slot, string file, string text)
        {
            string path = PathOf(slot, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string temp = path + TEMP_SUFFIX;
            byte[] bytes = Utf8.GetBytes(text);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            // File.Replace needs an existing target, so the very first write is a move
            if (File.Exists(path))
                File.Replace(temp, path, path + SaveFiles.BACKUP_SUFFIX);
            else
                File.Move(temp, path);
        }

        public void Quarantine(int slot, string file, bool backup)
        {
            string path = PathOf(slot, file) + (backup ? SaveFiles.BACKUP_SUFFIX : string.Empty);
            if (!File.Exists(path))
                return;

            string corrupt = path + SaveFiles.CORRUPT_SUFFIX;
            if (File.Exists(corrupt))
                File.Delete(corrupt);

            File.Move(path, corrupt);
        }

        public void DeleteSlot(int slot)
        {
            string directory = SlotDirectory(slot);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        private string SlotDirectory(int slot)
        {
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");

            return Path.Combine(Root, SLOT_PREFIX + slot.ToString(CultureInfo.InvariantCulture));
        }

        private string PathOf(int slot, string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentException("The file name must not be empty.", nameof(file));

            return Path.Combine(SlotDirectory(slot), file);
        }

        private static bool TryReadPath(string path, out string text)
        {
            if (!File.Exists(path))
            {
                text = null;
                return false;
            }

            text = File.ReadAllText(path, Utf8);
            return true;
        }
    }
}
