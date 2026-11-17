using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>A server in memory: answers at once, can be told to fail.</summary>
    internal sealed class FakeRemote : IRemoteSaveStorage
    {
        public readonly Dictionary<int, string> Packages = new();
        public bool Fail;
        public int Uploads;
        public Action WhileDownloading; // runs "during" the request: the game may save meanwhile

        public Task<IReadOnlyList<int>> ListAsync(CancellationToken token = default) =>
            Answer<IReadOnlyList<int>>(() => Packages.Keys.OrderBy(slot => slot).ToList());

        public Task<string> DownloadAsync(int slot, CancellationToken token = default) =>
            Answer(() =>
            {
                string package = Packages.TryGetValue(slot, out string found) ? found : null;
                WhileDownloading?.Invoke();
                return package;
            });

        public Task UploadAsync(int slot, string package, CancellationToken token = default) =>
            Answer(() =>
            {
                Uploads++;
                Packages[slot] = package;
                return true;
            });

        public Task DeleteAsync(int slot, CancellationToken token = default) => Answer(() => Packages.Remove(slot));

        private Task<T> Answer<T>(Func<T> answer) =>
            Fail ? Task.FromException<T>(new IOException("the server is down")) : Task.FromResult(answer());
    }

    public class SaveSyncTests
    {
        private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStorage _storage;
        private FakeRemote _remote;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _remote = new FakeRemote();
            _now = Start;
        }

        private SaveLoadService CreateService(InMemorySaveStorage storage = null) => new(storage ?? _storage, "1.0", () => _now);

        private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private SaveLoadService SaveCoins(int coins, InMemorySaveStorage storage = null)
        {
            SaveLoadService service = CreateService(storage);
            service.Load();
            service.Values.Set("coins", coins);
            service.Save();
            return service;
        }

        private int LoadCoins(InMemorySaveStorage storage = null)
        {
            SaveLoadService service = CreateService(storage);
            service.Load();
            return service.Values.Get("coins", -1);
        }

        [Test]
        public void Upload_ThenDownloadOnAnotherDevice_MovesTheSlot()
        {
            SaveLoadService service = SaveCoins(7);
            var sync = new SaveSync(service, _remote);
            Assert.AreEqual(SyncOutcome.Uploaded, Run(sync.UploadAsync(0)));

            var device = new InMemorySaveStorage();
            var other = new SaveSync(CreateService(device), _remote);
            Assert.AreEqual(SyncOutcome.Downloaded, Run(other.DownloadAsync(0)));

            Assert.AreEqual(7, LoadCoins(device));
        }

        [Test]
        public void UploadAndDownload_NothingThere_NothingToSync()
        {
            var sync = new SaveSync(CreateService(), _remote);

            Assert.AreEqual(SyncOutcome.NothingToSync, Run(sync.UploadAsync(3)));
            Assert.AreEqual(SyncOutcome.NothingToSync, Run(sync.DownloadAsync(3)));
        }

        [Test]
        public void Sync_NewerSideWins()
        {
            SaveCoins(1);
            var sync = new SaveSync(CreateService(), _remote);
            Assert.AreEqual(SyncOutcome.Uploaded, Run(sync.SyncAsync(0)), "only local: upload");
            Assert.AreEqual(SyncOutcome.UpToDate, Run(sync.SyncAsync(0)));

            var device = new InMemorySaveStorage();
            _now = Start.AddMinutes(5);
            SaveCoins(2, device);
            var deviceSync = new SaveSync(CreateService(device), _remote);
            Assert.AreEqual(SyncOutcome.Uploaded, Run(deviceSync.SyncAsync(0)), "the device is newer");

            Assert.AreEqual(SyncOutcome.Downloaded, Run(sync.SyncAsync(0)), "the server is newer now");
            Assert.AreEqual(2, LoadCoins());
        }

        [Test]
        public void Sync_LocalSavedWhileDownloading_LocalWins()
        {
            var device = new InMemorySaveStorage();
            _now = Start.AddMinutes(5);
            SaveCoins(2, device);
            Run(new SaveSync(CreateService(device), _remote).UploadAsync(0));
            _now = Start;
            SaveLoadService service = SaveCoins(1);
            _remote.WhileDownloading = () =>
            {
                _now = Start.AddMinutes(10);
                service.Values.Set("coins", 3);
                service.Save();
            };

            Assert.AreEqual(SyncOutcome.Uploaded, Run(new SaveSync(service, _remote).SyncAsync(0)));
            Assert.AreEqual(3, LoadCoins());
            Assert.IsFalse(service.IsReadOnly);
        }

        [Test]
        public void Sync_OnlyRemote_Downloads()
        {
            var device = new InMemorySaveStorage();
            SaveCoins(4, device);
            Run(new SaveSync(CreateService(device), _remote).UploadAsync(0));

            Assert.AreEqual(SyncOutcome.Downloaded, Run(new SaveSync(CreateService(), _remote).SyncAsync(0)));
            Assert.AreEqual(4, LoadCoins());
        }

        [Test]
        public void ServerDown_FailedAndLocalUntouched()
        {
            SaveCoins(9);
            _remote.Fail = true;
            var sync = new SaveSync(CreateService(), _remote);
            LogAssert.Expect(LogType.Warning, new Regex("server is down"));
            LogAssert.Expect(LogType.Warning, new Regex("server is down"));

            Assert.AreEqual(SyncOutcome.Failed, Run(sync.SyncAsync(0)));
            Assert.AreEqual(SyncOutcome.Failed, Run(sync.DownloadAsync(0)));
            Assert.AreEqual(9, LoadCoins());
        }

        [Test]
        public void UploadAfterSave_EverySaveGoesUp()
        {
            SaveLoadService service = CreateService();
            using var sync = new SaveSync(service, _remote) { UploadAfterSave = true };

            service.Values.Set("coins", 1);
            service.Save();
            service.Values.Set("coins", 2);
            service.Save(onlyIfChanged: true);
            service.Save(onlyIfChanged: true);

            Assert.AreEqual(2, _remote.Uploads, "an unchanged save uploads nothing");
            var device = new InMemorySaveStorage();
            Run(new SaveSync(CreateService(device), _remote).DownloadAsync(0));
            Assert.AreEqual(2, LoadCoins(device));
        }

        [Test]
        public void Dispose_StopsUploadingAfterSave()
        {
            SaveLoadService service = CreateService();
            var sync = new SaveSync(service, _remote) { UploadAfterSave = true };

            sync.Dispose();
            service.Values.Set("coins", 1);
            service.Save();

            Assert.AreEqual(0, _remote.Uploads);
        }

        [Test]
        public void DeleteRemote_RemovesOnlyTheServerCopy()
        {
            SaveCoins(3);
            var sync = new SaveSync(CreateService(), _remote);
            Run(sync.UploadAsync(0));

            Assert.AreEqual(SyncOutcome.Deleted, Run(sync.DeleteRemoteAsync(0)));

            CollectionAssert.IsEmpty(Run(sync.ListRemoteAsync()));
            Assert.AreEqual(3, LoadCoins());
        }

        [Test]
        public void Download_IntoCurrentSlot_NeedsLoad()
        {
            var device = new InMemorySaveStorage();
            SaveCoins(6, device);
            Run(new SaveSync(CreateService(device), _remote).UploadAsync(0));
            SaveLoadService service = SaveCoins(1);

            Run(new SaveSync(service, _remote).DownloadAsync(0));

            Assert.AreEqual(SaveOutcome.Blocked, service.Save(), "the old snapshot must not overwrite the download");
            service.Load();
            Assert.AreEqual(6, service.Values.Get("coins", 0));
        }

        [Test]
        public void FolderRemote_KeepsPackagesAsFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "SaveSyncTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveCoins(5);
                var folder = new FolderRemoteSaveStorage(root);
                Assert.AreEqual(SyncOutcome.Uploaded, Run(new SaveSync(CreateService(), folder).UploadAsync(0)));
                Assert.IsTrue(File.Exists(Path.Combine(root, "slot_0.json")));
                CollectionAssert.AreEqual(new[] { 0 }, Run(folder.ListAsync()));

                var device = new InMemorySaveStorage();
                Run(new SaveSync(CreateService(device), folder).DownloadAsync(0));
                Assert.AreEqual(5, LoadCoins(device));
                folder.DeleteAsync(0).GetAwaiter().GetResult();
                Assert.IsNull(Run(folder.DownloadAsync(0)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
    }
}
