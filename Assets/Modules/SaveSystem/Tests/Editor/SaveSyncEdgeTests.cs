using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>Server sync at its edges: the upload switch, nothing on either side, a server copy without meta.</summary>
    public class SaveSyncEdgeTests
    {
        private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStorage _storage;
        private FakeRemote _remote;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _remote = new FakeRemote();
        }

        private SaveLoadService CreateService() => new(_storage, "1.0", () => Start);

        private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        [Test]
        public void SaveSync_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveSync(null, _remote));
            Assert.Throws<ArgumentNullException>(() => new SaveSync(CreateService(), null));
        }

        [Test]
        public void UploadAfterSave_TurnedOnTwice_UploadsOncePerSave()
        {
            SaveLoadService service = CreateService();
            var sync = new SaveSync(service, _remote);

            sync.UploadAfterSave = true;
            sync.UploadAfterSave = true;
            service.Save();

            Assert.IsTrue(sync.UploadAfterSave);
            Assert.AreEqual(1, _remote.Uploads);

            sync.UploadAfterSave = false;
            Assert.IsFalse(sync.UploadAfterSave);
            service.Save();
            Assert.AreEqual(1, _remote.Uploads);
        }

        [Test]
        public void Sync_NothingOnEitherSide_NothingToSync()
        {
            var sync = new SaveSync(CreateService(), _remote);
            SyncOutcome? reported = null;
            sync.Synced += (slot, outcome) => reported = outcome;

            Assert.AreEqual(SyncOutcome.NothingToSync, Run(sync.SyncAsync(2)));

            Assert.AreEqual(SyncOutcome.NothingToSync, reported);
            Assert.AreEqual(0, _remote.Uploads);
        }

        [Test]
        public void Sync_ServerCopyWithoutMeta_LocalIsNewerAndUploaded()
        {
            SaveLoadService service = CreateService();
            service.Values.Set("coins", 3);
            service.Save();
            string local = service.ExportSlot(0);
            Assert.IsTrue(SaveJson.TryRead(local, out SlotPackage stripped));
            stripped.Files.Remove(SaveFiles.META);
            _remote.Packages[0] = SaveJson.Write(stripped);
            var sync = new SaveSync(service, _remote);

            Assert.AreEqual(SyncOutcome.Uploaded, Run(sync.SyncAsync(0)));

            Assert.AreEqual(local, _remote.Packages[0]);
        }
    }
}
