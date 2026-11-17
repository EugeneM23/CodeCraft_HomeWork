using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// Keeps slots in step with a server: the local storage stays the main one (saving never waits for the network),
    /// the server holds copies. Upload sends a slot, Download replaces the local one (Load it next), Sync moves the
    /// newer side by meta.json's save time. UploadAfterSave sends every written save. A failing server gives Failed and a
    /// warning; nothing local changes then. Continuations stay on Unity's main thread.
    /// </summary>
    public sealed class SaveSync : IDisposable
    {
        private readonly ISaveLoadService _saves;
        private readonly IRemoteSaveStorage _remote;
        private bool _uploadAfterSave;

        public SaveSync(ISaveLoadService saves, IRemoteSaveStorage remote)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        public bool UploadAfterSave
        {
            get => _uploadAfterSave;
            set
            {
                if (value == _uploadAfterSave)
                    return;

                _uploadAfterSave = value;
                if (value)
                    _saves.Saved += OnSaved;
                else
                    _saves.Saved -= OnSaved;
            }
        }

        /// <summary>Every finished sync with its slot: for a status line or a retry.</summary>
        public event Action<int, SyncOutcome> Synced;

        public void Dispose() => UploadAfterSave = false;

        public Task<SyncOutcome> UploadAsync(int slot, CancellationToken token = default) =>
            Guard(slot, async () =>
            {
                string package = _saves.ExportSlot(slot);
                if (package == null)
                    return SyncOutcome.NothingToSync;

                await _remote.UploadAsync(slot, package, token);
                return SyncOutcome.Uploaded;
            });

        public Task<SyncOutcome> DownloadAsync(int slot, CancellationToken token = default) =>
            Guard(slot, async () =>
            {
                string package = await _remote.DownloadAsync(slot, token);
                if (package == null)
                    return SyncOutcome.NothingToSync;

                _saves.ImportSlot(slot, package);
                return SyncOutcome.Downloaded;
            });

        public Task<SyncOutcome> SyncAsync(int slot, CancellationToken token = default) =>
            Guard(slot, async () =>
            {
                string remote = await _remote.DownloadAsync(slot, token);
                // Read after the answer: the game may have saved while the request was out
                string local = _saves.ExportSlot(slot);
                if (local == null && remote == null)
                    return SyncOutcome.NothingToSync;

                if (local == remote)
                    return SyncOutcome.UpToDate;

                if (remote == null || (local != null && SavedAt(local) >= SavedAt(remote)))
                {
                    await _remote.UploadAsync(slot, local, token);
                    return SyncOutcome.Uploaded;
                }

                _saves.ImportSlot(slot, remote);
                return SyncOutcome.Downloaded;
            });

        public Task<SyncOutcome> DeleteRemoteAsync(int slot, CancellationToken token = default) =>
            Guard(slot, async () =>
            {
                await _remote.DeleteAsync(slot, token);
                return SyncOutcome.Deleted;
            });

        public Task<IReadOnlyList<int>> ListRemoteAsync(CancellationToken token = default) => _remote.ListAsync(token);

        private async void OnSaved(SaveOutcome outcome)
        {
            if (outcome == SaveOutcome.Saved)
                await UploadAsync(_saves.CurrentSlot); // Guard never throws: async void is safe here
        }

        private async Task<SyncOutcome> Guard(int slot, Func<Task<SyncOutcome>> work)
        {
            SyncOutcome outcome;
            try
            {
                outcome = await work();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Syncing save slot {slot} with the server failed: {exception.Message}");
                outcome = SyncOutcome.Failed;
            }

            Synced?.Invoke(slot, outcome);
            return outcome;
        }

        private static DateTime SavedAt(string package)
        {
            if (SaveJson.TryRead(package, out SlotPackage read) && read.Files != null
                && read.Files.TryGetValue(SaveFiles.META, out string metaText) && SaveJson.TryRead(metaText, out SlotMeta meta))
                return SlotMeta.ParseTime(meta.SavedAtUtc);

            return DateTime.MinValue;
        }
    }
}
