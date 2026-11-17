using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>What every storage promises: atomic writes with a backup, quarantine, slot listing and deletion.</summary>
    public abstract class SaveStorageContractTests
    {
        protected ISaveStorage Storage { get; private set; }

        protected abstract ISaveStorage CreateStorage();
        protected abstract void Plant(int slot, string file, string text);
        protected abstract bool Exists(int slot, string file);

        [SetUp]
        public void SetUpStorage() => Storage = CreateStorage();

        [Test]
        public void Storage_MissingFile_ReadsNothing() => Assert.IsFalse(Storage.TryRead(0, SaveFiles.SAVE, out _));

        [Test]
        public void Storage_FirstWrite_ReadsBackWithoutBackup()
        {
            Storage.Write(0, SaveFiles.SAVE, "first");

            Assert.IsTrue(Storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.AreEqual("first", text);
            Assert.IsFalse(Storage.TryReadBackup(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Storage_SecondWrite_PreviousBecomesBackup()
        {
            Storage.Write(0, SaveFiles.SAVE, "first");
            Storage.Write(0, SaveFiles.SAVE, "second");

            Storage.TryRead(0, SaveFiles.SAVE, out string text);
            Assert.IsTrue(Storage.TryReadBackup(0, SaveFiles.SAVE, out string backup));
            Assert.AreEqual("second", text);
            Assert.AreEqual("first", backup);
        }

        [Test]
        public void Storage_QuarantineMain_RenamesToCorrupt()
        {
            Plant(0, SaveFiles.SAVE, "broken");

            Storage.Quarantine(0, SaveFiles.SAVE, backup: false);

            Assert.IsFalse(Storage.TryRead(0, SaveFiles.SAVE, out _));
            Assert.IsTrue(Exists(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
        }

        [Test]
        public void Storage_QuarantineBackup_RenamesToCorrupt()
        {
            Storage.Write(0, SaveFiles.SAVE, "first");
            Storage.Write(0, SaveFiles.SAVE, "second");

            Storage.Quarantine(0, SaveFiles.SAVE, backup: true);

            Assert.IsFalse(Storage.TryReadBackup(0, SaveFiles.SAVE, out _));
            Assert.IsTrue(Exists(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX + SaveFiles.CORRUPT_SUFFIX));
            Assert.IsTrue(Storage.TryRead(0, SaveFiles.SAVE, out _), "the main file stays");
        }

        [Test]
        public void Storage_ListSlots_SlotsWithFilesAscending()
        {
            Storage.Write(3, SaveFiles.SAVE, "three");
            Storage.Write(1, SaveFiles.SAVE, "one");

            CollectionAssert.AreEqual(new[] { 1, 3 }, Storage.ListSlots());
        }

        [Test]
        public void Storage_DeleteSlot_RemovesItsFilesOnly()
        {
            Storage.Write(1, SaveFiles.SAVE, "one");
            Storage.Write(2, SaveFiles.SAVE, "two");

            Storage.DeleteSlot(1);

            Assert.IsFalse(Storage.TryRead(1, SaveFiles.SAVE, out _));
            CollectionAssert.AreEqual(new[] { 2 }, Storage.ListSlots());
        }
    }

    public class InMemorySaveStorageTests : SaveStorageContractTests
    {
        private InMemorySaveStorage _memory;

        protected override ISaveStorage CreateStorage() => _memory = new InMemorySaveStorage();
        protected override void Plant(int slot, string file, string text) => _memory.Put(slot, file, text);
        protected override bool Exists(int slot, string file) => _memory.Has(slot, file);
    }

    public class FileSaveStorageTests : SaveStorageContractTests
    {
        private string _root;

        protected override ISaveStorage CreateStorage()
        {
            _root = Path.Combine(Path.GetTempPath(), "SaveSystemTests_" + Guid.NewGuid().ToString("N"));
            return new FileSaveStorage(_root);
        }

        protected override void Plant(int slot, string file, string text)
        {
            string path = PathOf(slot, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        protected override bool Exists(int slot, string file) => File.Exists(PathOf(slot, file));

        protected string PathOf(int slot, string file) => Path.Combine(_root, $"slot_{slot}", file);

        [TearDown]
        public void DeleteRoot()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void FileStorage_WriteFails_PreviousFileIntact()
        {
            Storage.Write(0, SaveFiles.SAVE, "first");

            // A locked temp file makes the next write fail before it touches the save
            using (new FileStream(PathOf(0, SaveFiles.SAVE) + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Assert.Throws<IOException>(() => Storage.Write(0, SaveFiles.SAVE, "second"));
            }

            Storage.TryRead(0, SaveFiles.SAVE, out string text);
            Assert.AreEqual("first", text);
        }

        [Test]
        public void FileStorage_DeleteSlot_RemovesFolder()
        {
            Storage.Write(4, SaveFiles.SAVE, "four");

            Storage.DeleteSlot(4);

            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "slot_4")));
        }

        [Test]
        public void FileStorage_ServiceWithBrokenMain_RecoversFromBackup()
        {
            var service = new SaveLoadService(Storage, "1.0");
            var value = new FlakySerializer("value") { Value = 1 };
            service.Register(value);
            service.Save();
            value.Value = 2;
            service.Save();
            File.WriteAllText(PathOf(0, SaveFiles.SAVE), "{ broken");

            var reader = new SaveLoadService(new FileSaveStorage(_root), "1.0");
            var read = new FlakySerializer("value");
            reader.Register(read);
            LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));

            Assert.AreEqual(LoadOutcome.Recovered, reader.Load());
            Assert.AreEqual(1, read.Value);
            Assert.IsTrue(File.Exists(PathOf(0, SaveFiles.SAVE) + SaveFiles.CORRUPT_SUFFIX));
        }
    }
}
