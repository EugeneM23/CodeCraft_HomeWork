using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>Storages and transforms at their edges: bad arguments, missing roots and files, broken text.</summary>
    public class SaveStorageEdgeTests
    {
        /// <summary>A key-value store that keeps the default Flush, which does nothing.</summary>
        private sealed class UnflushedStorage : KeyValueSaveStorage
        {
            public readonly Dictionary<string, string> Data = new();

            protected override bool TryGet(string key, out string text) => Data.TryGetValue(key, out text);
            protected override void Set(string key, string text) => Data[key] = text;
            protected override void Remove(string key) => Data.Remove(key);
        }

        private sealed class NamedTransform : ISaveTransform
        {
            public NamedTransform(string id) => Id = id;

            public string Id { get; }
            public byte[] Encode(byte[] data) => data;
            public byte[] Decode(byte[] data) => data;
        }

        private string _root;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "SaveSystemEdgeTests_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string PathOf(int slot, string file) => Path.Combine(_root, $"slot_{slot}", file);

        // ---------- FileSaveStorage ----------

        [Test]
        public void FileStorage_EmptyRoot_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(""));
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(null));
        }

        [Test]
        public void FileStorage_RootNotThereYet_NoSlots()
        {
            var storage = new FileSaveStorage(_root);

            CollectionAssert.IsEmpty(storage.ListSlots());
            Assert.IsFalse(Directory.Exists(_root), "listing does not create the folder");
        }

        [Test]
        public void FileStorage_BadSlotOrFileName_Throws()
        {
            var storage = new FileSaveStorage(_root);

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.Write(-1, SaveFiles.SAVE, "text"));
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.DeleteSlot(-1));
            Assert.Throws<ArgumentException>(() => storage.TryRead(0, "", out _));
            Assert.Throws<ArgumentException>(() => storage.Write(0, null, "text"));
        }

        [Test]
        public void FileStorage_QuarantineMissingFile_DoesNothing()
        {
            var storage = new FileSaveStorage(_root);

            Assert.DoesNotThrow(() => storage.Quarantine(0, SaveFiles.SAVE, backup: false));

            Assert.IsFalse(File.Exists(PathOf(0, SaveFiles.SAVE) + SaveFiles.CORRUPT_SUFFIX));
        }

        [Test]
        public void FileStorage_QuarantineAgain_NewestBrokenFileKept()
        {
            var storage = new FileSaveStorage(_root);
            storage.Write(0, SaveFiles.SAVE, "first broken");
            storage.Quarantine(0, SaveFiles.SAVE, backup: false);
            storage.Write(0, SaveFiles.SAVE, "second broken");

            storage.Quarantine(0, SaveFiles.SAVE, backup: false);

            Assert.IsFalse(File.Exists(PathOf(0, SaveFiles.SAVE)));
            Assert.AreEqual("second broken", File.ReadAllText(PathOf(0, SaveFiles.SAVE) + SaveFiles.CORRUPT_SUFFIX));
        }

        // ---------- InMemorySaveStorage ----------

        [Test]
        public void InMemoryStorage_QuarantineMissingFile_DoesNothing()
        {
            var storage = new InMemorySaveStorage();

            storage.Quarantine(0, SaveFiles.SAVE, backup: true);

            Assert.IsFalse(storage.Has(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX + SaveFiles.CORRUPT_SUFFIX));
            CollectionAssert.IsEmpty(storage.ListSlots());
        }

        // ---------- KeyValueSaveStorage ----------

        [Test]
        public void KeyValueStorage_BadSlotOrFileName_Throws()
        {
            var storage = new UnflushedStorage();

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.KeyOf(-1, SaveFiles.SAVE));
            Assert.Throws<ArgumentException>(() => storage.KeyOf(0, ""));
            Assert.Throws<ArgumentException>(() => storage.Write(0, null, "text"));
        }

        [Test]
        public void KeyValueStorage_DefaultFlush_StoresAndReadsBack()
        {
            var storage = new UnflushedStorage();

            storage.Write(1, SaveFiles.SAVE, "one");

            Assert.IsTrue(storage.TryRead(1, SaveFiles.SAVE, out string text));
            Assert.AreEqual("one", text);
            CollectionAssert.AreEqual(new[] { 1 }, storage.ListSlots());
        }

        [Test]
        public void KeyValueStorage_QuarantineMissingFile_IndexUntouched()
        {
            var storage = new UnflushedStorage();
            storage.Write(1, SaveFiles.META, "meta");
            string index = storage.Data[KeyValueSaveStorage.DEFAULT_PREFIX + "index"];

            storage.Quarantine(1, SaveFiles.SAVE, backup: false);

            Assert.AreEqual(index, storage.Data[KeyValueSaveStorage.DEFAULT_PREFIX + "index"]);
            Assert.IsFalse(storage.Data.ContainsKey(storage.KeyOf(1, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX)));
        }

        // ---------- TransformSaveStorage ----------

        [Test]
        public void Transform_MarkerWithoutColon_BrokenHeader()
        {
            var storage = new TransformSaveStorage(new InMemorySaveStorage(), new GZipSaveTransform());

            var exception = Assert.Throws<InvalidDataException>(() => storage.Decode(TransformSaveStorage.MARKER + "gzip"));

            StringAssert.Contains("broken transform header", exception.Message);
        }

        [Test]
        public void Transform_BodyNotBase64_CannotBeDecoded()
        {
            var storage = new TransformSaveStorage(new InMemorySaveStorage(), new GZipSaveTransform());

            var exception = Assert.Throws<InvalidDataException>(() =>
                storage.Decode(TransformSaveStorage.MARKER + "gzip:not base64 at all!"));

            StringAssert.Contains("could not be decoded", exception.Message);
            Assert.IsNotNull(exception.InnerException);
        }

        [TestCase("")]
        [TestCase("g-zip")]
        [TestCase("aes+gzip")]
        public void Transform_IdNotLettersAndDigits_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => new TransformSaveStorage(new InMemorySaveStorage(), new NamedTransform(id)));
            Assert.Throws<ArgumentException>(() => new TransformSaveStorage(new InMemorySaveStorage()).AddReader(new NamedTransform(id)));
        }

        // ---------- AesSaveTransform ----------

        [Test]
        public void Aes_EmptyPassword_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AesSaveTransform(""));
            Assert.Throws<ArgumentException>(() => new AesSaveTransform(null));
        }

        [Test]
        public void Aes_DataShorterThanIvAndMac_Throws()
        {
            var aes = new AesSaveTransform("secret");

            Assert.Throws<InvalidDataException>(() => aes.Decode(new byte[40]));
            Assert.Throws<InvalidDataException>(() => aes.Decode(null));
        }

        // ---------- FolderRemoteSaveStorage ----------

        [Test]
        public void FolderRemote_EmptyFolder_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FolderRemoteSaveStorage(""));
            Assert.Throws<ArgumentException>(() => new FolderRemoteSaveStorage(null));
        }

        [Test]
        public void FolderRemote_DeleteUploaded_FileGone()
        {
            var remote = new FolderRemoteSaveStorage(_root);
            remote.UploadAsync(2, "{}").GetAwaiter().GetResult();
            remote.UploadAsync(4, "{}").GetAwaiter().GetResult();

            remote.DeleteAsync(2).GetAwaiter().GetResult();
            remote.DeleteAsync(7).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { 4 }, remote.ListAsync().GetAwaiter().GetResult());
            Assert.IsNull(remote.DownloadAsync(2).GetAwaiter().GetResult());
        }
    }
}
