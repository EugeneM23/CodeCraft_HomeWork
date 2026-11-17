using System.IO;
using System.Text;
using NUnit.Framework;

namespace Saving.Tests
{
    public class TransformSaveStorageTests : SaveStorageContractTests
    {
        private InMemorySaveStorage _inner;

        protected override ISaveStorage CreateStorage()
        {
            _inner = new InMemorySaveStorage();
            return new TransformSaveStorage(_inner, new GZipSaveTransform(), new AesSaveTransform("secret"));
        }

        protected override void Plant(int slot, string file, string text) => _inner.Put(slot, file, text);
        protected override bool Exists(int slot, string file) => _inner.Has(slot, file);

        [Test]
        public void Transform_WrittenText_IsNotPlain()
        {
            Storage.Write(0, SaveFiles.SAVE, "{ \"coins\": 120 }");

            _inner.TryRead(0, SaveFiles.SAVE, out string raw);
            StringAssert.StartsWith(TransformSaveStorage.MARKER + "gzip+aes:", raw);
            StringAssert.DoesNotContain("coins", raw);
        }

        [Test]
        public void Transform_PlainTextFromBefore_StillReads()
        {
            _inner.Put(0, SaveFiles.SAVE, "{ \"plain\": true }");

            Assert.IsTrue(Storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.AreEqual("{ \"plain\": true }", text);
        }

        [Test]
        public void Transform_WrongKey_ThrowsInsteadOfReadingGarbage()
        {
            Storage.Write(0, SaveFiles.SAVE, "{ \"coins\": 120 }");
            var other = new TransformSaveStorage(_inner, new GZipSaveTransform(), new AesSaveTransform("another"));

            Assert.Throws<InvalidDataException>(() => other.TryRead(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Transform_TamperedText_Throws()
        {
            Storage.Write(0, SaveFiles.SAVE, "{ \"coins\": 120 }");
            _inner.TryRead(0, SaveFiles.SAVE, out string raw);
            char[] chars = raw.ToCharArray();
            int index = chars.Length - 5;
            chars[index] = chars[index] == 'A' ? 'B' : 'A';
            _inner.Put(0, SaveFiles.SAVE, new string(chars));

            Assert.Throws<InvalidDataException>(() => Storage.TryRead(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Transform_OnlyCompression_ReadsWithoutKey()
        {
            var zipped = new TransformSaveStorage(_inner, new GZipSaveTransform());
            string big = new StringBuilder().Insert(0, "{ \"a\": 1 } ", 200).ToString();

            zipped.Write(1, SaveFiles.SAVE, big);

            _inner.TryRead(1, SaveFiles.SAVE, out string raw);
            Assert.Less(raw.Length, big.Length / 4);
            zipped.TryRead(1, SaveFiles.SAVE, out string text);
            Assert.AreEqual(big, text);
        }

        [Test]
        public void Transform_MissingStep_Throws()
        {
            Storage.Write(0, SaveFiles.SAVE, "{ }");
            var zipOnly = new TransformSaveStorage(_inner, new GZipSaveTransform());

            Assert.Throws<InvalidDataException>(() => zipOnly.TryRead(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Transform_StepTurnedOff_ReaderStillDecodes()
        {
            new TransformSaveStorage(_inner, new GZipSaveTransform()).Write(1, SaveFiles.SAVE, "{ \"a\": 1 }");
            var plain = new TransformSaveStorage(_inner);
            plain.AddReader(new GZipSaveTransform());

            Assert.IsTrue(plain.TryRead(1, SaveFiles.SAVE, out string text));
            Assert.AreEqual("{ \"a\": 1 }", text);
            plain.Write(2, SaveFiles.SAVE, "{ }");
            _inner.TryRead(2, SaveFiles.SAVE, out string raw);
            Assert.AreEqual("{ }", raw, "a reader only reads: new files are written with the steps alone");
        }

        [Test]
        public void Transform_NoStepsNoReader_MarkedTextThrowsInsteadOfPassingAsPlain()
        {
            Storage.Write(0, SaveFiles.SAVE, "{ }");

            Assert.Throws<InvalidDataException>(() => new TransformSaveStorage(_inner).TryRead(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Transform_PlainTextRefused_WhenAsked()
        {
            _inner.Put(0, SaveFiles.SAVE, "{ \"edited\": true }");
            var strict = new TransformSaveStorage(_inner, new AesSaveTransform("secret")) { AcceptPlainText = false };

            Assert.Throws<InvalidDataException>(() => strict.TryRead(0, SaveFiles.SAVE, out _));
        }

        [Test]
        public void Transform_WrongKeyUnderService_LoadFailsAndKeepsFile()
        {
            var service = new SaveLoadService(Storage, "1.0");
            service.Values.Set("coins", 5);
            service.Save();
            var wrong = new SaveLoadService(new TransformSaveStorage(_inner, new GZipSaveTransform(),
                new AesSaveTransform("another")), "1.0");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("not loaded"));

            Assert.AreEqual(LoadOutcome.Failed, wrong.Load());
            Assert.IsTrue(wrong.IsReadOnly);
            Assert.IsTrue(_inner.Has(0, SaveFiles.SAVE), "nothing is quarantined");
            CollectionAssert.IsEmpty(wrong.GetSlots(), "an unreadable meta is skipped");
        }

        [Test]
        public void Transform_UnderService_RoundTrips()
        {
            var service = new SaveLoadService(Storage, "1.0");
            service.Values.Set("coins", 5);
            service.SlotName = "Hero";
            service.Save();

            var reader = new SaveLoadService(Storage, "1.0");
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(5, reader.Values.Get("coins", 0));
            Assert.AreEqual("Hero", reader.GetSlot(0).Name);
        }
    }
}
