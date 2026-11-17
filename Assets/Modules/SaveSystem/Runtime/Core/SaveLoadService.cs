using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// Save slots over an <see cref="ISaveStorage"/>. Keeps the current slot's sections in memory: Save refreshes the
    /// sections of registered serializers and leaves the rest alone, so data of systems that are gone right now
    /// survives. Load checks every registered section first and applies only when all of them can be read; a
    /// serializer registered later gets its section at once. A slot written by a newer game, or one that failed to
    /// migrate or to read, turns read-only and is never overwritten. meta.json carries time, play time and summary.
    /// An attached layer (the objects layer) adds its own sections and may finish a load later, when the saved scene is
    /// restored: until then the service is loading. Values (the "$vars" section) belong to the slot like the
    /// sections. Slots are managed here too: names, copies, the latest and a free one, export and import as one string;
    /// writing another slot's files into the current one makes it read-only until the next Load. LoadInPlace is a load
    /// that keeps the scenes in play (KeepsScene tells the layer).
    /// </summary>
    public sealed class SaveLoadService : ISaveLoadService
    {
        private enum ReadStatus
        {
            Missing,
            Corrupt,
            Newer,
            Ok
        }

        private readonly ISaveStorage _storage;
        private readonly string _gameVersion;
        private readonly Func<DateTime> _utcNow;
        private readonly List<IGameSerializer> _serializers = new();
        private readonly Dictionary<string, IGameSerializer> _byKey = new();
        private readonly Dictionary<string, string> _summary = new();
        private readonly SaveValues _values = new();
        private readonly string _emptyText;

        private Dictionary<string, SaveSection> _sections = new();
        private string _lastText;
        private ISaveExtension _extension;
        private string _slotName = string.Empty;
        private string _createdAtUtc;

        public SaveLoadService(ISaveStorage storage, string gameVersion, Func<DateTime> utcNow = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _gameVersion = gameVersion ?? string.Empty;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _emptyText = WriteSaveFile(new Dictionary<string, SaveSection>());
            _lastText = _emptyText;
        }

        public int CurrentSlot { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsReadOnly { get; private set; }
        public double PlayTime { get; private set; }
        public IDictionary<string, string> Summary => _summary;
        public SaveValues Values => _values;

        public string SlotName
        {
            get => _slotName;
            set => _slotName = value ?? string.Empty;
        }

        /// <summary>The active scene's name for meta.json; the save system component sets it.</summary>
        public Func<string> SceneName { get; set; }

        /// <summary>
        /// True while a <see cref="LoadInPlace"/> load runs (until it finishes or returns): the objects layer keeps the
        /// scenes in play.
        /// </summary>
        public bool KeepsScene { get; private set; }

        /// <summary>
        /// True while a <see cref="LoadAtSceneStart"/> load runs: the scene that has just started may take its data in
        /// place, even if some of its objects have run their Start.
        /// </summary>
        public bool AtSceneStart { get; private set; }

        public event Action<SaveOutcome> Saved;
        public event Action<LoadOutcome> Loaded;
        public event Action SlotsChanged;

        public void Register(IGameSerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            if (string.IsNullOrEmpty(serializer.Key))
                throw new ArgumentException("A serializer needs a key.", nameof(serializer));

            if (_byKey.ContainsKey(serializer.Key))
                throw new ArgumentException($"A serializer with key '{serializer.Key}' is already registered.",
                    nameof(serializer));

            _byKey.Add(serializer.Key, serializer);
            _serializers.Add(serializer);

            if (_sections.TryGetValue(serializer.Key, out SaveSection section))
                ApplyOnRegister(serializer, section);
        }

        /// <summary>Stops saving the serializer; its last captured section stays in the snapshot.</summary>
        public void Unregister(IGameSerializer serializer)
        {
            if (serializer == null || !_byKey.TryGetValue(serializer.Key, out IGameSerializer current)
                || !ReferenceEquals(current, serializer))
                return;

            _byKey.Remove(serializer.Key);
            _serializers.Remove(serializer);
        }

        /// <summary>Stops saving whatever is registered under the key; its last captured section stays.</summary>
        public void Unregister(string key)
        {
            if (key != null && _byKey.TryGetValue(key, out IGameSerializer serializer))
                Unregister(serializer);
        }

        /// <summary>Registered serializers and the layer -> snapshot, without writing: call before unloading what they save.</summary>
        public void Capture()
        {
            if (IsLoading)
                return;

            CaptureSections();
        }

        public SaveOutcome Save(bool onlyIfChanged = false)
        {
            SaveOutcome outcome = SaveCurrentSlot(onlyIfChanged);
            Saved?.Invoke(outcome);
            if (outcome == SaveOutcome.Saved)
                SlotsChanged?.Invoke();

            return outcome;
        }

        public LoadOutcome Load()
        {
            if (IsLoading)
                throw new InvalidOperationException("Load cannot be called while a load is in progress.");

            IsLoading = true;
            LoadOutcome outcome;
            bool continues;
            try
            {
                outcome = LoadCurrentSlot(out continues);
            }
            catch (Exception exception)
            {
                outcome = Refuse(LoadOutcome.Failed, $"the storage failed: {exception.Message}");
                continues = false;
            }

            if (continues)
                BeginExtensionLoad(outcome);
            else
                FinishLoad(outcome);

            return outcome;
        }

        /// <summary>
        /// Reads the current slot into the snapshot ahead of <see cref="Load"/>, quietly: the serializers registered
        /// so far get their data now, every one registered later at once. Nothing is written, moved aside or reported,
        /// no event is raised — a missing, broken or newer file is left to the Load that follows. True when read.
        /// </summary>
        public bool Preload()
        {
            if (IsLoading || ReadSaveFile(backup: false, out SaveFile file, out _) != ReadStatus.Ok)
                return false;

            _sections = new Dictionary<string, SaveSection>(file.Sections);
            if (_sections.TryGetValue(SaveValues.KEY, out SaveSection values))
                ApplyOnRegister(_values, values);

            foreach (IGameSerializer serializer in _serializers.ToArray())
            {
                if (_sections.TryGetValue(serializer.Key, out SaveSection section))
                    ApplyOnRegister(serializer, section);
            }

            return true;
        }

        /// <summary>
        /// A load that keeps the scenes in play: the objects layer neither loads nor requires the saved scene — the active
        /// one gets its data in place if it was scanned this frame and nothing in it has started (otherwise it keeps its
        /// state, and the load still reports Loaded), other scenes when they load. A save system made by the first
        /// Saveable starts this way; buttons, menus and <see cref="Load"/> go to the saved scene.
        /// </summary>
        public LoadOutcome LoadInPlace()
        {
            KeepsScene = true;
            try
            {
                return Load();
            }
            finally
            {
                KeepsScene = false;
            }
        }

        /// <summary>
        /// <see cref="Load"/> from the start of a scene (Load On Start of a system placed in it): when the saved scene is
        /// the one that has just started, its objects get their data in place instead of the scene being loaded again.
        /// </summary>
        public LoadOutcome LoadAtSceneStart()
        {
            AtSceneStart = true;
            try
            {
                return Load();
            }
            finally
            {
                AtSceneStart = false;
            }
        }

        /// <summary>
        /// True while a <see cref="LoadLive"/> load runs: the objects layer restores the scenes in play as they are —
        /// no scene is loaded again unless the save cannot be reached that way.
        /// </summary>
        public bool RestoresLive { get; private set; }

        /// <summary>
        /// <see cref="Load"/> without loading the scenes again: the objects in play go back to their saved state, those
        /// made since the save are removed, saved ones that are gone are made again. For games whose systems must not
        /// be restarted by a scene load (DI containers, pools). When the save is of another scene, or an object of the
        /// scene itself destroyed since must come back, the layer loads the saved scene as Load does.
        /// </summary>
        public LoadOutcome LoadLive()
        {
            RestoresLive = true;
            try
            {
                return Load();
            }
            finally
            {
                RestoresLive = false;
            }
        }

        public void SelectSlot(int slot, bool keepData = false)
        {
            ValidateSlot(slot);
            if (IsLoading)
                throw new InvalidOperationException("SelectSlot cannot be called while a load is in progress.");

            CurrentSlot = slot;
            IsReadOnly = false;

            if (keepData)
            {
                _lastText = null; // the new slot's file is unknown: the next save always writes
                _createdAtUtc = null;
            }
            else
            {
                StartEmpty();
            }
        }

        public IReadOnlyList<SlotInfo> GetSlots()
        {
            var slots = new List<SlotInfo>();
            foreach (int slot in _storage.ListSlots())
            {
                if (TryReadMeta(slot, out SlotMeta meta))
                    slots.Add(new SlotInfo(slot, meta));
            }

            return slots;
        }

        public SlotInfo GetSlot(int slot)
        {
            ValidateSlot(slot);
            return TryReadMeta(slot, out SlotMeta meta) ? new SlotInfo(slot, meta) : null;
        }

        public bool HasSlot(int slot) => slot >= 0 && _storage.ListSlots().Contains(slot);

        public bool RenameSlot(int slot, string name)
        {
            ValidateSlot(slot);
            name ??= string.Empty;
            bool hasMeta = TryReadMeta(slot, out SlotMeta meta);
            // A newer game's meta has fields this build does not know: writing it back would lose them
            if (hasMeta && meta.FormatVersion > SaveFile.CURRENT_FORMAT_VERSION)
                return false;

            bool current = slot == CurrentSlot;
            if (current)
                _slotName = name;

            if (!hasMeta)
            {
                if (current)
                    SlotsChanged?.Invoke();

                return current;
            }

            meta.Name = name;
            _storage.Write(slot, SaveFiles.META, SaveJson.Write(meta));
            SlotsChanged?.Invoke();
            return true;
        }

        public bool CopySlot(int from, int to)
        {
            ValidateSlot(from);
            ValidateSlot(to);
            if (from == to)
                throw new ArgumentException("A slot cannot be copied into itself.", nameof(to));

            string package = ExportSlot(from);
            if (package == null)
                return false;

            ImportSlot(to, package);
            return true;
        }

        public int GetLatestSlot()
        {
            int latest = -1;
            DateTime latestTime = DateTime.MinValue;
            foreach (SlotInfo info in GetSlots())
            {
                if (latest >= 0 && info.SavedAtUtc <= latestTime)
                    continue;

                latest = info.Slot;
                latestTime = info.SavedAtUtc;
            }

            return latest;
        }

        public int GetFreeSlot(int from = 0)
        {
            var used = new HashSet<int>(_storage.ListSlots());
            int slot = Math.Max(0, from);
            while (used.Contains(slot))
                slot++;

            return slot;
        }

        public void DeleteSlot(int slot)
        {
            ValidateSlot(slot);
            _storage.DeleteSlot(slot);

            if (slot == CurrentSlot)
                StartEmpty();

            SlotsChanged?.Invoke();
        }

        public string ExportSlot(int slot)
        {
            ValidateSlot(slot);
            var package = new SlotPackage { FormatVersion = SlotPackage.CURRENT_FORMAT_VERSION };
            foreach (string file in SaveFiles.Packaged)
            {
                // A file that does not even parse is left behind for its backup, as Load would do
                if ((_storage.TryRead(slot, file, out string text) && SaveJson.TryParseObject(text, out _))
                    || (_storage.TryReadBackup(slot, file, out text) && SaveJson.TryParseObject(text, out _)))
                    package.Files[file] = text;
            }

            if (!package.Files.ContainsKey(SaveFiles.SAVE))
                return null;

            return JsonConvert.SerializeObject(package, Formatting.None, SaveJson.Settings);
        }

        public void ImportSlot(int slot, string package)
        {
            ValidateSlot(slot);
            if (string.IsNullOrEmpty(package))
                throw new ArgumentException("The package is empty.", nameof(package));

            if (!SaveJson.TryRead(package, out SlotPackage read) || read.Files == null
                || read.FormatVersion < 1 || read.FormatVersion > SlotPackage.CURRENT_FORMAT_VERSION
                || !read.Files.TryGetValue(SaveFiles.SAVE, out string save) || !SaveJson.TryParseObject(save, out _))
                throw new FormatException("The text is not a save slot package of this version.");

            // Only the known files: a file name from outside must never become a path
            foreach (string file in SaveFiles.Packaged)
            {
                if (read.Files.TryGetValue(file, out string text) && text != null)
                    _storage.Write(slot, file, text);
            }

            // Without its meta the target would keep the meta of whatever it held before
            if (!read.Files.ContainsKey(SaveFiles.META))
                _storage.Write(slot, SaveFiles.META, SaveJson.Write(new SlotMeta
                {
                    FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                    SavedAtUtc = SlotMeta.FormatTime(_utcNow())
                }));

            if (slot == CurrentSlot)
                IsReadOnly = true; // the snapshot in memory is the old slot: saving would write it over the import

            SlotsChanged?.Invoke();
        }

        /// <summary>Adds played time; the save system calls it every frame with game time.</summary>
        public void AddPlayTime(double seconds)
        {
            if (seconds > 0)
                PlayTime += seconds;
        }

        /// <summary>Plugs a layer in (the objects layer); null takes it out. Not during a load.</summary>
        public void Attach(ISaveExtension extension)
        {
            if (IsLoading)
                throw new InvalidOperationException("Attach cannot be called while a load is in progress.");

            _extension = extension;
        }

        /// <summary>A section of the current snapshot: the layer reads its scene sections with it.</summary>
        public bool TryGetSection(string key, out SaveSection section) => _sections.TryGetValue(key, out section);

        // ---------- Save ----------

        private SaveOutcome SaveCurrentSlot(bool onlyIfChanged)
        {
            if (IsLoading || IsReadOnly)
                return SaveOutcome.Blocked;

            CaptureSections();
            string text = WriteSaveFile(_sections);
            if (onlyIfChanged && text == _lastText)
                return SaveOutcome.Unchanged;

            string now = SlotMeta.FormatTime(_utcNow());
            try
            {
                _storage.Write(CurrentSlot, SaveFiles.SAVE, text);
                _storage.Write(CurrentSlot, SaveFiles.META, WriteMeta(now));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Saving slot {CurrentSlot} failed: {exception.Message}");
                return SaveOutcome.Failed;
            }

            _lastText = text;
            _createdAtUtc ??= now;
            return SaveOutcome.Saved;
        }

        private void CaptureSections()
        {
            SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
            // Empty values leave no section: an untouched slot stays an empty envelope
            if (_values.Count > 0)
                SerializerGroup.CaptureInto(new IGameSerializer[] { _values }, _sections);
            else
                _sections.Remove(SaveValues.KEY);

            CaptureExtension();
        }

        private string WriteSaveFile(Dictionary<string, SaveSection> sections) =>
            SaveJson.Write(new SaveFile
            {
                FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                GameVersion = _gameVersion,
                Sections = sections
            });

        private string WriteMeta(string now) =>
            SaveJson.Write(new SlotMeta
            {
                FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                GameVersion = _gameVersion,
                Name = _slotName,
                CreatedAtUtc = _createdAtUtc ?? now,
                SavedAtUtc = now,
                Scene = CurrentSceneName(),
                PlayTimeSeconds = PlayTime,
                Summary = new Dictionary<string, string>(_summary)
            });

        private string CurrentSceneName()
        {
            try
            {
                return SceneName?.Invoke() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>A slot's meta.json; false when it is missing or unreadable, also when the storage throws.</summary>
        private bool TryReadMeta(int slot, out SlotMeta meta)
        {
            meta = null;
            try
            {
                return _storage.TryRead(slot, SaveFiles.META, out string text) && SaveJson.TryRead(text, out meta);
            }
            catch (Exception)
            {
                meta = null;
                return false;
            }
        }

        // ---------- Load ----------

        private LoadOutcome LoadCurrentSlot(out bool continues)
        {
            continues = false;
            ReadStatus main = ReadSaveFile(backup: false, out SaveFile file, out string text);
            if (main == ReadStatus.Newer)
                return Refuse(LoadOutcome.NewerVersion, "it was written by a newer version of the game");

            LoadOutcome outcome = LoadOutcome.Loaded;
            if (main != ReadStatus.Ok)
            {
                if (main == ReadStatus.Corrupt)
                    _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: false);

                ReadStatus backup = ReadSaveFile(backup: true, out file, out text);
                if (backup == ReadStatus.Newer)
                    return Refuse(LoadOutcome.NewerVersion, "its backup was written by a newer version of the game");

                if (backup != ReadStatus.Ok)
                {
                    if (backup == ReadStatus.Corrupt)
                        _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: true);

                    StartEmpty();
                    if (main == ReadStatus.Missing && backup == ReadStatus.Missing)
                        return LoadOutcome.Empty;

                    Debug.LogWarning($"Save slot {CurrentSlot} is corrupted, starting over; the broken files were " +
                                     $"renamed to *{SaveFiles.CORRUPT_SUFFIX}.");
                    return LoadOutcome.Corrupted;
                }

                outcome = LoadOutcome.Recovered;
            }

            var prepared = new List<(IGameSerializer Serializer, JToken Data)>();
            bool migrated = false;
            foreach (IGameSerializer serializer in _serializers)
            {
                if (!file.Sections.TryGetValue(serializer.Key, out SaveSection section))
                    continue;

                SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                if (status == SectionStatus.NewerVersion)
                    return Refuse(LoadOutcome.NewerVersion, problem);

                if (status == SectionStatus.MigrationFailed)
                    return Refuse(LoadOutcome.MigrationFailed, problem);

                migrated |= section.V < serializer.Version;
                prepared.Add((serializer, data));
            }

            if (file.Sections.TryGetValue(SaveValues.KEY, out SaveSection valuesSection))
            {
                SectionStatus status = SerializerGroup.Prepare(_values, valuesSection, out JToken data, out string problem);
                if (status == SectionStatus.NewerVersion)
                    return Refuse(LoadOutcome.NewerVersion, problem);

                if (status == SectionStatus.MigrationFailed)
                    return Refuse(LoadOutcome.MigrationFailed, problem);

                prepared.Add((_values, data));
            }

            if (_extension != null)
            {
                LoadOutcome check;
                string refusal;
                try
                {
                    check = _extension.Check(file.Sections, out refusal);
                }
                catch (Exception exception)
                {
                    return Refuse(LoadOutcome.Failed, $"checking the saved scenes failed: {exception.Message}");
                }

                if (check != LoadOutcome.Loaded)
                    return Refuse(check, refusal);
            }

            if (outcome == LoadOutcome.Recovered)
                Debug.LogWarning($"Save slot {CurrentSlot} was restored from its backup " +
                                 $"({SaveFiles.SAVE}{SaveFiles.BACKUP_SUFFIX}).");

            if (migrated)
                KeepPremigrationCopy(text);

            ReadMeta();
            _sections = new Dictionary<string, SaveSection>(file.Sections);
            // After a recovery the main file is gone: the next save must write it even if nothing changed
            _lastText = outcome == LoadOutcome.Recovered ? null : text;
            IsReadOnly = false;
            _values.Clear(); // a file without values has none; with them, the restore fills them
            SerializerGroup.RestoreInOrder(prepared);
            continues = _extension != null;
            return outcome;
        }

        private ReadStatus ReadSaveFile(bool backup, out SaveFile file, out string text)
        {
            file = null;
            bool found = backup
                ? _storage.TryReadBackup(CurrentSlot, SaveFiles.SAVE, out text)
                : _storage.TryRead(CurrentSlot, SaveFiles.SAVE, out text);

            if (!found)
                return ReadStatus.Missing;

            // The version goes first: a newer format may have a shape this build cannot parse
            if (!SaveJson.TryParseObject(text, out JObject json) || json["formatVersion"]?.Type != JTokenType.Integer)
                return ReadStatus.Corrupt;

            int formatVersion = (int)json["formatVersion"];
            if (formatVersion > SaveFile.CURRENT_FORMAT_VERSION)
                return ReadStatus.Newer;

            if (formatVersion < 1)
                return ReadStatus.Corrupt;

            try
            {
                file = json.ToObject<SaveFile>(SaveJson.Serializer);
            }
            catch (Exception)
            {
                return ReadStatus.Corrupt;
            }

            if (file?.Sections == null)
                return ReadStatus.Corrupt;

            foreach (SaveSection section in file.Sections.Values)
            {
                if (section == null)
                    return ReadStatus.Corrupt;
            }

            return ReadStatus.Ok;
        }

        /// <summary>Play time, summary and name from meta.json; a missing or broken meta starts them from zero.</summary>
        private void ReadMeta()
        {
            _summary.Clear();
            PlayTime = 0;
            _slotName = string.Empty;
            _createdAtUtc = null;

            string text;
            try
            {
                if (!_storage.TryRead(CurrentSlot, SaveFiles.META, out text))
                    return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{SaveFiles.META} of save slot {CurrentSlot} is unreadable: {exception.Message}");
                return;
            }

            if (!SaveJson.TryRead(text, out SlotMeta meta))
            {
                Debug.LogWarning($"{SaveFiles.META} of save slot {CurrentSlot} is unreadable; play time and summary " +
                                 "start from zero.");
                return;
            }

            PlayTime = Math.Max(0, meta.PlayTimeSeconds);
            _slotName = meta.Name ?? string.Empty;
            _createdAtUtc = string.IsNullOrEmpty(meta.CreatedAtUtc) ? meta.SavedAtUtc : meta.CreatedAtUtc;
            if (meta.Summary == null)
                return;

            foreach (KeyValuePair<string, string> pair in meta.Summary)
                _summary[pair.Key] = pair.Value;
        }

        private void ApplyOnRegister(IGameSerializer serializer, SaveSection section)
        {
            bool wasLoading = IsLoading;
            IsLoading = true;
            try
            {
                SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                if (status != SectionStatus.Ok)
                {
                    IsReadOnly = true;
                    Debug.LogError($"Save slot {CurrentSlot} becomes read-only: {problem}.");
                    return;
                }

                SerializerGroup.RestoreInOrder(new[] { (serializer, data) });
            }
            finally
            {
                IsLoading = wasLoading;
            }
        }

        private void CaptureExtension()
        {
            if (_extension == null)
                return;

            try
            {
                _extension.CaptureInto(_sections);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Saving the scene sections failed, their previous data is kept: {exception.Message}");
            }
        }

        private void BeginExtensionLoad(LoadOutcome outcome)
        {
            try
            {
                _extension.BeginLoad(() => FinishLoad(outcome));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Restoring the saved scene of slot {CurrentSlot} failed: {exception.Message}");
                FinishLoad(outcome);
            }
        }

        /// <summary>Ends the load once: a second call (a late callback of the layer) does nothing.</summary>
        private void FinishLoad(LoadOutcome outcome)
        {
            if (!IsLoading)
                return;

            IsLoading = false;
            KeepsScene = false; // a Loaded subscriber that loads again gets an ordinary load
            RestoresLive = false;
            Loaded?.Invoke(outcome);
        }

        private LoadOutcome Refuse(LoadOutcome outcome, string problem)
        {
            IsReadOnly = true;
            Debug.LogError($"Save slot {CurrentSlot} is not loaded and will not be overwritten: {problem}.");
            return outcome;
        }

        private void KeepPremigrationCopy(string text)
        {
            try
            {
                if (!_storage.TryRead(CurrentSlot, SaveFiles.PREMIGRATION, out _))
                    _storage.Write(CurrentSlot, SaveFiles.PREMIGRATION, text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not keep the pre-migration copy of save slot {CurrentSlot}: {exception.Message}");
            }
        }

        private void StartEmpty()
        {
            _sections = new Dictionary<string, SaveSection>();
            _summary.Clear();
            _values.Clear();
            _slotName = string.Empty;
            _createdAtUtc = null;
            PlayTime = 0;
            _lastText = _emptyText;
            IsReadOnly = false;
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");
        }
    }
}
