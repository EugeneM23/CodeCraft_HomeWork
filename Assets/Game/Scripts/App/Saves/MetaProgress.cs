using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Saving;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What the player has earned: currencies (credits among them), tank modules and their levels, the module in each
    /// slot, unlocked and completed levels, plus the teams' own data (<see cref="Get{T}"/> / <see cref="Set{T}"/>).
    /// Kept in its own save (slot 0, section "progress"). Every change raises <see cref="Changed"/> and makes the
    /// progress dirty; ProgressAutosave writes it.
    /// </summary>
    public sealed class PlayerProgress : IPlayerProgress
    {
        public const int SLOT = 0;
        public const string CREDITS = "credits";

        private readonly SaveLoadService _saves;

        public PlayerProgress(ISaveStorage storage, string gameVersion)
        {
            Data = new ProgressData();
            _saves = new SaveLoadService(storage, gameVersion);
            _saves.Register(new ProgressSerializer(this));
        }

        /// <summary>The raw data. After changing it directly call <see cref="MarkDirty"/>.</summary>
        public ProgressData Data { get; private set; }

        public bool IsDirty { get; private set; }

        /// <summary>True when the file was written by a newer game or failed to migrate: Save writes nothing.</summary>
        public bool IsReadOnly => _saves.IsReadOnly;

        public event Action Changed;

        // ---------- Currencies ----------

        public int Credits => GetCurrency(CREDITS);

        public void AddCredits(int amount) => AddCurrency(CREDITS, amount);

        public bool TrySpendCredits(int amount) => TrySpend(CREDITS, amount);

        public int GetCurrency(string id) => id != null && Data.currencies.TryGetValue(id, out int amount) ? amount : 0;

        public void AddCurrency(string id, int amount)
        {
            ValidateId(id);
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use TrySpend to take currency away.");

            if (amount == 0)
                return;

            long sum = (long)GetCurrency(id) + amount;
            Data.currencies[id] = sum > int.MaxValue ? int.MaxValue : (int)sum;
            MarkDirty();
        }

        /// <summary>Takes the amount if there is enough; otherwise changes nothing and returns false.</summary>
        public bool TrySpend(string id, int amount)
        {
            ValidateId(id);
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "The amount must not be negative.");

            int current = GetCurrency(id);
            if (current < amount)
                return false;

            if (amount == 0)
                return true;

            Data.currencies[id] = current - amount;
            MarkDirty();
            return true;
        }

        // ---------- Modules ----------

        public bool OwnsModule(string moduleId) => GetModuleLevel(moduleId) > 0;

        public int GetModuleLevel(string moduleId) =>
            moduleId != null && Data.modules.TryGetValue(moduleId, out int level) ? level : 0;

        /// <summary>0 takes the module away.</summary>
        public void SetModuleLevel(string moduleId, int level)
        {
            ValidateId(moduleId);
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level), level, "The level must not be negative.");

            if (GetModuleLevel(moduleId) == level)
                return;

            if (level == 0)
                Data.modules.Remove(moduleId);
            else
                Data.modules[moduleId] = level;

            MarkDirty();
        }

        public string GetInstalledModule(string slot) =>
            slot != null && Data.installedModules.TryGetValue(slot, out string moduleId) ? moduleId : null;

        /// <summary>Null or empty module id empties the slot.</summary>
        public void InstallModule(string slot, string moduleId)
        {
            ValidateId(slot);
            if (string.IsNullOrEmpty(moduleId))
                moduleId = null;

            if (GetInstalledModule(slot) == moduleId)
                return;

            if (moduleId == null)
                Data.installedModules.Remove(slot);
            else
                Data.installedModules[slot] = moduleId;

            MarkDirty();
        }

        // ---------- Levels ----------

        public IReadOnlyCollection<string> UnlockedLevels => Data.unlockedLevels;

        public IReadOnlyCollection<string> CompletedLevels => Data.completedLevels;

        public bool IsLevelUnlocked(string levelId) => levelId != null && Data.unlockedLevels.Contains(levelId);

        public void UnlockLevel(string levelId)
        {
            ValidateId(levelId);
            if (Data.unlockedLevels.Contains(levelId))
                return;

            Data.unlockedLevels.Add(levelId);
            MarkDirty();
        }

        public bool IsLevelCompleted(string levelId) => levelId != null && Data.completedLevels.Contains(levelId);

        /// <summary>Does not unlock anything: what opens next is the design's call.</summary>
        public void MarkLevelCompleted(string levelId)
        {
            ValidateId(levelId);
            if (Data.completedLevels.Contains(levelId))
                return;

            Data.completedLevels.Add(levelId);
            MarkDirty();
        }

        // ---------- Extensions ----------

        public bool Has(string key) => key != null && Data.extensions.ContainsKey(key);

        /// <summary>
        /// A team's data under its key, as a fresh copy (default when there is none or it no longer fits T): change the
        /// copy and give it back with <see cref="Set{T}"/>.
        /// </summary>
        public T Get<T>(string key)
        {
            if (key == null || !Data.extensions.TryGetValue(key, out JToken token))
                return default;

            try
            {
                return SaveJson.FromToken<T>(token);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Progress extension '{key}' does not read as {typeof(T).Name}: {exception.Message}");
                return default;
            }
        }

        /// <summary>Stores a team's data under its key (a copy is kept); null removes it.</summary>
        public void Set<T>(string key, T value)
        {
            ValidateId(key);
            if (value == null)
            {
                if (Data.extensions.Remove(key))
                    MarkDirty();

                return;
            }

            Data.extensions[key] = SaveJson.ToToken(value);
            MarkDirty();
        }

        // ---------- Saving ----------

        /// <summary>Marks the progress as changed: after editing <see cref="Data"/> directly.</summary>
        public void MarkDirty()
        {
            IsDirty = true;
            Changed?.Invoke();
        }

        /// <summary>Writes the file if it differs from the last write; Saved or Unchanged clears <see cref="IsDirty"/>.</summary>
        public SaveOutcome Save()
        {
            SaveOutcome outcome = _saves.Save(onlyIfChanged: true);
            if (outcome == SaveOutcome.Saved || outcome == SaveOutcome.Unchanged)
                IsDirty = false;

            return outcome;
        }

        /// <summary>
        /// Reads the file. No file or a broken one gives an empty progress; a file of a newer game is not applied and
        /// makes the save read-only. Raises Changed, leaves the progress clean.
        /// </summary>
        public LoadOutcome Load()
        {
            LoadOutcome outcome = _saves.Load();
            if (outcome == LoadOutcome.Empty || outcome == LoadOutcome.Corrupted)
                Restore(new ProgressData());

            IsDirty = false;
            return outcome;
        }

        /// <summary>
        /// A new game: empty progress, dirty, so the next save overwrites the file — even one written by a newer game
        /// that made the save read-only.
        /// </summary>
        public void ResetProgress()
        {
            _saves.SelectSlot(SLOT);
            Data = new ProgressData();
            MarkDirty();
        }

        /// <summary>The serializer hands over the section read by Load.</summary>
        internal void Restore(ProgressData data)
        {
            data ??= new ProgressData();
            data.Normalize();
            Data = data;
            Changed?.Invoke();
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("The id must not be empty.", nameof(id));
        }
    }
}
