using System;
using System.Collections.Generic;
using Saving;

namespace Game
{
    /// <summary>
    /// The player's progress between runs (GDD §6, §9, §14) over <see cref="SaveData"/> and the <see cref="GameCatalog"/>:
    /// its own save (slot 0, section "progress", version 2). Every IMetaProgress change writes the save at once; the
    /// legacy IPlayerProgress calls of the old menu only make it dirty and ProgressAutosave writes them.
    ///
    /// Legacy mapping (until the menu moves to IMetaProgress): Credits are the marks. A module id the catalog knows is
    /// that module (level 0 — not owned); any other id is the old menu's own and lives in <see cref="SaveData.legacy"/>.
    /// Slots are names: one that parses to a <see cref="ModuleSlot"/> (<see cref="ModuleSlotNames"/>) and a catalog
    /// module of that slot go to the selected tank's loadout; anything else (the menu's "Hull", its own module ids)
    /// is kept in legacy under the slot name, and a legacy entry wins when reading the slot back. A level id equal to
    /// a chapter id is that chapter; other level ids are kept in legacy.
    /// </summary>
    public sealed class MetaProgress : IMetaProgress, IPlayerProgress
    {
        public const int SLOT = 0;

        private readonly SaveLoadService _saves;
        private readonly GameCatalog _catalog;
        private bool _restored;

        /// <param name="catalog">May be null: then nothing is for sale and only the legacy calls keep data.</param>
        public MetaProgress(ISaveStorage storage, string gameVersion, GameCatalog catalog)
        {
            _catalog = catalog;
            Data = SaveDataDefaults.Create(catalog);
            _saves = new SaveLoadService(storage, gameVersion);
            _saves.Register(new ProgressSerializer(this));
        }

        public event Action Changed;

        public GameCatalog Catalog => _catalog;

        /// <summary>The raw data, for the serializer and tests. Change it only through the methods.</summary>
        public SaveData Data { get; private set; }

        /// <summary>Changed since the last write (the legacy calls; a refused write).</summary>
        public bool IsDirty { get; private set; }

        /// <summary>True when the file was written by a newer game or failed to migrate: Save writes nothing.</summary>
        public bool IsReadOnly => _saves.IsReadOnly;

        public bool HasSave { get; private set; }

        public int Marks => Data.marks;

        public int Samples => Data.samples;

        public TankId SelectedTank => new(Data.selectedTank);

        public ProgressStats Stats => Data.stats.ToStats();

        // ---------- Reading ----------

        public bool OwnsTank(TankId tank) => Data.FindTank(tank.Value) is { owned: true };

        public bool OwnsModule(ModuleId module) => Data.FindModule(module.Value) is { owned: true };

        public int GetModuleLevel(ModuleId module) =>
            Data.FindModule(module.Value) is { owned: true } entry ? entry.level : 0;

        public ModuleId GetInstalled(TankId tank, ModuleSlot slot)
        {
            if (Data.FindTank(tank.Value) is { } entry
                && entry.loadout.TryGetValue(ModuleSlotNames.Key(slot), out string module)
                && !string.IsNullOrEmpty(module))
                return new ModuleId(module);

            TankConfig config = FindTankConfig(tank);
            return config != null ? config.DefaultModule(slot) : default;
        }

        public ChapterProgress GetChapter(ChapterId chapter) =>
            Data.FindChapter(chapter.Value) is { } entry
                ? new ChapterProgress(entry.unlocked, entry.completed, entry.bestArena, entry.bestTimeSec)
                : default;

        public bool HasTutorialFlag(string flag) => !string.IsNullOrEmpty(flag) && Data.tutorialFlags.Contains(flag);

        // ---------- Purchases (GDD §6, §8) ----------

        public PurchaseResult CanBuyTank(TankId tank)
        {
            TankConfig config = FindTankConfig(tank);
            if (config == null)
                return PurchaseResult.Unknown;

            if (OwnsTank(tank))
                return PurchaseResult.AlreadyOwned;

            // Milestone 1: a starting tank is never sold, an unpriced one is not for sale yet
            if (config.IsStarting || (config.PriceMarks <= 0 && config.PriceSamples <= 0))
                return PurchaseResult.Locked;

            return Afford(config.PriceMarks, config.PriceSamples);
        }

        public PurchaseResult CanBuyModule(ModuleId module)
        {
            ModuleConfig config = FindModuleConfig(module);
            if (config == null)
                return PurchaseResult.Unknown;

            if (OwnsModule(module))
                return PurchaseResult.AlreadyOwned;

            if (!config.InShop && !config.IsStandard)
                return PurchaseResult.Locked;

            return Afford(config.PriceMarks, config.PriceSamples);
        }

        public PurchaseResult CanUpgradeModule(ModuleId module)
        {
            ModuleConfig config = FindModuleConfig(module);
            if (config == null)
                return PurchaseResult.Unknown;

            int level = GetModuleLevel(module);
            if (level <= 0)
                return PurchaseResult.NotOwned;

            if (level >= Math.Min(config.LevelCap, ModuleConfig.MAX_LEVEL))
                return PurchaseResult.MaxLevel;

            ModuleConfig.Cost cost = config.UpgradeCost(level + 1);
            return Afford(cost.marks, cost.samples);
        }

        public PurchaseResult BuyTank(TankId tank)
        {
            PurchaseResult check = CanBuyTank(tank);
            if (check != PurchaseResult.Done)
                return check;

            TankConfig config = FindTankConfig(tank);
            Pay(config.PriceMarks, config.PriceSamples);
            GetOrAddTank(tank).owned = true;
            SaveDataDefaults.Reconcile(Data, _catalog); // the loadout of a tank the save did not know
            Commit();
            return PurchaseResult.Done;
        }

        public PurchaseResult BuyModule(ModuleId module)
        {
            PurchaseResult check = CanBuyModule(module);
            if (check != PurchaseResult.Done)
                return check;

            ModuleConfig config = FindModuleConfig(module);
            Pay(config.PriceMarks, config.PriceSamples);
            ModuleSaveData entry = GetOrAddModule(module.Value);
            entry.owned = true;
            entry.level = 1;
            Commit();
            return PurchaseResult.Done;
        }

        public PurchaseResult UpgradeModule(ModuleId module)
        {
            PurchaseResult check = CanUpgradeModule(module);
            if (check != PurchaseResult.Done)
                return check;

            ModuleConfig config = FindModuleConfig(module);
            ModuleSaveData entry = GetOrAddModule(module.Value);
            ModuleConfig.Cost cost = config.UpgradeCost(entry.level + 1);
            Pay(cost.marks, cost.samples);
            entry.level++;
            Commit();
            return PurchaseResult.Done;
        }

        // ---------- Hangar ----------

        public bool SelectTank(TankId tank)
        {
            if (!OwnsTank(tank))
                return false;

            if (Data.selectedTank == tank.Value)
                return true;

            Data.selectedTank = tank.Value;
            Commit();
            return true;
        }

        public bool Install(TankId tank, ModuleSlot slot, ModuleId module)
        {
            ModuleConfig config = FindModuleConfig(module);
            if (!OwnsTank(tank) || config == null || config.Slot != slot || !OwnsModule(module))
                return false;

            if (GetInstalled(tank, slot) == module)
                return true;

            GetOrAddTank(tank).loadout[ModuleSlotNames.Key(slot)] = module.Value;
            Commit();
            return true;
        }

        public void SetTutorialFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || Data.tutorialFlags.Contains(flag))
                return;

            Data.tutorialFlags.Add(flag);
            Commit();
        }

        // ---------- Runs ----------

        public void ApplyRun(RunResult result)
        {
            Data.marks = AddClamped(Data.marks, result.marks);
            Data.samples = AddClamped(Data.samples, result.samples);

            if (!result.chapter.IsNone)
            {
                ChapterSaveData chapter = GetOrAddChapter(result.chapter.Value);
                chapter.bestArena = Math.Max(chapter.bestArena, result.arenaReached);
                if (result.outcome == RunOutcome.Victory)
                {
                    chapter.unlocked = true;
                    chapter.completed = true;
                    if (result.timeSec > 0f && (chapter.bestTimeSec <= 0f || result.timeSec < chapter.bestTimeSec))
                        chapter.bestTimeSec = result.timeSec;

                    UnlockChaptersAfter(result.chapter);
                }
            }

            StatsSaveData stats = Data.stats;
            stats.runs = AddClamped(stats.runs, 1);
            stats.kills = AddClamped(stats.kills, result.kills);
            stats.crushes = AddClamped(stats.crushes, result.crushes);
            stats.elitesKilled = AddClamped(stats.elitesKilled, result.elitesKilled);
            stats.bossesKilled = AddClamped(stats.bossesKilled, result.bossesKilled);
            if (result.timeSec > 0f)
                stats.playTimeSec += result.timeSec;

            Commit();
        }

        // ---------- Saving ----------

        void IMetaProgress.Save() => Save();

        /// <summary>Writes the file if it differs from the last write; Saved or Unchanged clears <see cref="IsDirty"/>.</summary>
        public SaveOutcome Save()
        {
            SaveOutcome outcome = _saves.Save(onlyIfChanged: true);
            if (outcome == SaveOutcome.Saved || outcome == SaveOutcome.Unchanged)
                IsDirty = false;

            if (outcome == SaveOutcome.Saved)
                HasSave = true;

            return outcome;
        }

        /// <summary>
        /// Reads the file (a version 1 file is migrated). No file or a broken one gives a new game; a file of a newer
        /// game is not applied (a new game in memory) and makes the save read-only until <see cref="NewGame"/>.
        /// Raises Changed, leaves the progress clean.
        /// </summary>
        public LoadOutcome Load()
        {
            _restored = false;
            LoadOutcome outcome = _saves.Load();
            if (!_restored)
                Data = SaveDataDefaults.Create(_catalog);

            _restored = false;
            HasSave = outcome == LoadOutcome.Loaded || outcome == LoadOutcome.Recovered
                                                    || outcome == LoadOutcome.NewerVersion
                                                    || outcome == LoadOutcome.MigrationFailed;
            IsDirty = false;
            Changed?.Invoke();
            return outcome;
        }

        /// <summary>A new game: the defaults, written at once, even over a file of a newer game.</summary>
        public void NewGame()
        {
            _saves.SelectSlot(SLOT);
            Data = SaveDataDefaults.Create(_catalog);
            Commit();
        }

        /// <summary>The serializer hands over the section read by Load.</summary>
        internal void Restore(SaveData data)
        {
            data ??= new SaveData();
            SaveDataDefaults.Reconcile(data, _catalog);
            Data = data;
            _restored = true;
        }

        // ---------- Legacy IPlayerProgress (the old menu) ----------

        public int Credits => Data.marks;

        public void AddCredits(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use TrySpendCredits to take credits away.");

            if (amount == 0)
                return;

            Data.marks = AddClamped(Data.marks, amount);
            MarkDirty();
        }

        public bool TrySpendCredits(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "The amount must not be negative.");

            if (Data.marks < amount)
                return false;

            if (amount == 0)
                return true;

            Data.marks -= amount;
            MarkDirty();
            return true;
        }

        public bool OwnsModule(string moduleId) => GetModuleLevel(moduleId) > 0;

        public int GetModuleLevel(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId))
                return 0;

            if (IsCatalogModule(moduleId))
                return GetModuleLevel(new ModuleId(moduleId));

            return Data.legacy.modules.TryGetValue(moduleId, out int level) ? level : 0;
        }

        /// <summary>0 takes the module away; a catalog module's level is capped at 3.</summary>
        public void SetModuleLevel(string moduleId, int level)
        {
            ValidateId(moduleId);
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level), level, "The level must not be negative.");

            if (IsCatalogModule(moduleId))
                level = Math.Min(level, ModuleConfig.MAX_LEVEL);

            if (GetModuleLevel(moduleId) == level)
                return;

            if (IsCatalogModule(moduleId))
            {
                ModuleSaveData entry = GetOrAddModule(moduleId);
                entry.owned = level > 0;
                entry.level = level;
            }
            else if (level == 0)
            {
                Data.legacy.modules.Remove(moduleId);
            }
            else
            {
                Data.legacy.modules[moduleId] = level;
            }

            MarkDirty();
        }

        /// <summary>A legacy entry for the slot name first, then the selected tank's loadout; null when none.</summary>
        public string GetInstalledModule(string slot)
        {
            if (string.IsNullOrEmpty(slot))
                return null;

            if (Data.legacy.installed.TryGetValue(slot, out string legacy))
                return legacy;

            if (!ModuleSlotNames.TryParse(slot, out ModuleSlot parsed))
                return null;

            ModuleId installed = GetInstalled(SelectedTank, parsed);
            return installed.IsNone ? null : installed.Value;
        }

        /// <summary>
        /// An owned catalog module that fits the slot goes into the selected tank (and drops a legacy entry of that
        /// slot name); anything else is kept in legacy. Null or empty empties the legacy entry.
        /// </summary>
        public void InstallModule(string slot, string moduleId)
        {
            ValidateId(slot);
            bool changed;
            if (string.IsNullOrEmpty(moduleId))
            {
                changed = Data.legacy.installed.Remove(slot);
            }
            else if (FitsSelectedTank(slot, moduleId, out ModuleSlot parsed))
            {
                changed = Data.legacy.installed.Remove(slot);
                TankSaveData tank = GetOrAddTank(SelectedTank);
                string key = ModuleSlotNames.Key(parsed);
                if (!tank.loadout.TryGetValue(key, out string current) || current != moduleId)
                {
                    tank.loadout[key] = moduleId;
                    changed = true;
                }
            }
            else
            {
                changed = !Data.legacy.installed.TryGetValue(slot, out string current) || current != moduleId;
                Data.legacy.installed[slot] = moduleId;
            }

            if (changed)
                MarkDirty();
        }

        /// <summary>Unlocked chapters (by id), then the legacy level ids. A fresh list on every call.</summary>
        public IReadOnlyCollection<string> UnlockedLevels
        {
            get
            {
                var levels = new List<string>();
                foreach (ChapterSaveData chapter in Data.chapters)
                {
                    if (chapter.unlocked)
                        levels.Add(chapter.id);
                }

                foreach (string level in Data.legacy.unlockedLevels)
                {
                    if (!levels.Contains(level))
                        levels.Add(level);
                }

                return levels;
            }
        }

        public bool IsLevelUnlocked(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return false;

            // The chapter's real state shows through; the menu's own marks stay its own
            return Data.legacy.unlockedLevels.Contains(levelId) ||
                   (IsCatalogChapter(levelId) && Data.FindChapter(levelId) is { unlocked: true });
        }

        public void UnlockLevel(string levelId)
        {
            ValidateId(levelId);
            if (IsLevelUnlocked(levelId))
                return;

            // Never into the chapters: a chapter opens only by its RequiredChapter (ApplyRun), not by an old menu
            // path that happens to use the same id
            Data.legacy.unlockedLevels.Add(levelId);

            MarkDirty();
        }

        public bool IsLevelCompleted(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return false;

            return Data.legacy.completedLevels.Contains(levelId) ||
                   (IsCatalogChapter(levelId) && Data.FindChapter(levelId) is { completed: true });
        }

        /// <summary>Does not unlock anything (the old contract): the menu unlocks what follows.</summary>
        public void MarkLevelCompleted(string levelId)
        {
            ValidateId(levelId);
            if (IsLevelCompleted(levelId))
                return;

            // Never into the chapters: only a won run completes one (ApplyRun)
            Data.legacy.completedLevels.Add(levelId);

            MarkDirty();
        }

        // ---------- Inner ----------

        /// <summary>A change of the legacy calls: dirty, Changed; the autosave writes it.</summary>
        private void MarkDirty()
        {
            IsDirty = true;
            Changed?.Invoke();
        }

        /// <summary>A change of the IMetaProgress calls: written at once, then Changed.</summary>
        private void Commit()
        {
            IsDirty = true;
            Save();
            Changed?.Invoke();
        }

        private PurchaseResult Afford(int marks, int samples)
        {
            if (Data.marks < marks)
                return PurchaseResult.NotEnoughMarks;

            if (Data.samples < samples)
                return PurchaseResult.NotEnoughSamples;

            return PurchaseResult.Done;
        }

        private void Pay(int marks, int samples)
        {
            Data.marks -= Math.Max(0, marks);
            Data.samples -= Math.Max(0, samples);
        }

        private void UnlockChaptersAfter(ChapterId completed)
        {
            if (_catalog == null)
                return;

            foreach (ChapterConfig config in _catalog.Chapters)
            {
                if (config != null && config.RequiredChapter != null && config.RequiredChapter.Id == completed)
                    GetOrAddChapter(config.Id.Value).unlocked = true;
            }
        }

        private bool FitsSelectedTank(string slot, string moduleId, out ModuleSlot parsed)
        {
            parsed = default;
            ModuleConfig config = FindModuleConfig(new ModuleId(moduleId));
            return config != null && ModuleSlotNames.TryParse(slot, out parsed) && config.Slot == parsed
                   && OwnsModule(config.Id) && OwnsTank(SelectedTank);
        }

        private TankConfig FindTankConfig(TankId tank) =>
            _catalog != null && !tank.IsNone ? _catalog.FindTank(tank) : null;

        private ModuleConfig FindModuleConfig(ModuleId module) =>
            _catalog != null && !module.IsNone ? _catalog.FindModule(module) : null;

        private bool IsCatalogModule(string moduleId) => FindModuleConfig(new ModuleId(moduleId)) != null;

        private bool IsCatalogChapter(string chapterId) =>
            _catalog != null && !string.IsNullOrEmpty(chapterId) && _catalog.FindChapter(new ChapterId(chapterId)) != null;

        private TankSaveData GetOrAddTank(TankId tank)
        {
            TankSaveData entry = Data.FindTank(tank.Value);
            if (entry != null)
                return entry;

            entry = new TankSaveData { id = tank.Value };
            Data.tanks.Add(entry);
            return entry;
        }

        private ModuleSaveData GetOrAddModule(string moduleId)
        {
            ModuleSaveData entry = Data.FindModule(moduleId);
            if (entry != null)
                return entry;

            entry = new ModuleSaveData { id = moduleId };
            Data.modules.Add(entry);
            return entry;
        }

        private ChapterSaveData GetOrAddChapter(string chapterId)
        {
            ChapterSaveData entry = Data.FindChapter(chapterId);
            if (entry != null)
                return entry;

            entry = new ChapterSaveData { id = chapterId };
            Data.chapters.Add(entry);
            return entry;
        }

        private static int AddClamped(int value, int amount)
        {
            if (amount <= 0)
                return value;

            long sum = (long)value + amount;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("The id must not be empty.", nameof(id));
        }
    }
}
