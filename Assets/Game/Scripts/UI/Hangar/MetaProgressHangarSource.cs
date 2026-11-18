using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// IHangarSource over the core's save (IMetaProgress) and catalog (GameCatalog). The progress decides: prices,
    /// money checks and locks come from IMetaProgress's Can*/Buy*/Upgrade* answers, which also write the save.
    /// The numbers come from the combat team's TankStatsUseCase. Changed is IMetaProgress.Changed passed on.
    /// Lists are rebuilt on the first read after a change. Dispose unsubscribes from the progress.
    /// </summary>
    public sealed class MetaProgressHangarSource : IHangarSource, IDisposable
    {
        public event Action Changed;

        private readonly IMetaProgress _meta;
        private readonly GameCatalog _catalog;
        private readonly IGameFlow _flow;
        private readonly Action _onMetaChanged;
        private readonly List<HangarModule>[] _slotModules = new List<HangarModule>[HangarSlots.Ids.Length];
        private readonly List<HangarChapter> _chapters = new();
        private readonly List<HangarModule> _none = new();
        private readonly List<ModuleLevel> _loadout = new(TankConfig.SLOT_COUNT);
        private bool _listsDirty = true;

        /// <summary>The flow StartChapter calls; null: AppServices.GameFlow at the time of the call.</summary>
        public MetaProgressHangarSource(IMetaProgress meta, GameCatalog catalog, IGameFlow flow = null)
        {
            _meta = meta ?? throw new ArgumentNullException(nameof(meta));
            _catalog = catalog;
            _flow = flow;
            for (int i = 0; i < _slotModules.Length; i++)
                _slotModules[i] = new List<HangarModule>();

            _onMetaChanged = OnMetaChanged;
            _meta.Changed += _onMetaChanged;
        }

        /// <summary>The progress this adapter reads (AppServices compares it to re-create the adapter).</summary>
        public IMetaProgress Meta => _meta;

        public int Marks => _meta.Marks;
        public int Samples => _meta.Samples;
        public bool HasSave => _meta.HasSave;
        public string SelectedTankId => _meta.SelectedTank.Value;
        public IReadOnlyList<string> Slots => HangarSlots.Ids;

        public IReadOnlyList<HangarChapter> Chapters
        {
            get
            {
                EnsureLists();
                return _chapters;
            }
        }

        private IReadOnlyList<ModuleConfig> CatalogModules =>
            _catalog != null ? _catalog.Modules : Array.Empty<ModuleConfig>();

        private IReadOnlyList<ChapterConfig> CatalogChapters =>
            _catalog != null ? _catalog.Chapters : Array.Empty<ChapterConfig>();

        public void Dispose() => _meta.Changed -= _onMetaChanged;

        public void NewGame() => _meta.NewGame();

        public string InstalledIn(string slotId)
        {
            int index = HangarSlots.IndexOf(slotId);
            if (index < 0)
                return null;

            ModuleId installed = _meta.GetInstalled(_meta.SelectedTank, (ModuleSlot)index);
            return installed.IsNone ? null : installed.Value;
        }

        public IReadOnlyList<HangarModule> ModulesFor(string slotId)
        {
            int index = HangarSlots.IndexOf(slotId);
            if (index < 0)
                return _none;

            EnsureLists();
            return _slotModules[index];
        }

        public HangarModule Module(string moduleId)
        {
            ModuleConfig module = FindModule(moduleId);
            return module != null ? Build(module) : default;
        }

        public HangarTankStats Stats(string replacementModuleId = null)
        {
            TankConfig tank = FindTank();
            ModuleConfig replacement = FindModule(replacementModuleId);

            FillLoadout(_meta, _meta.SelectedTank, replacement, _loadout);
            TankCombatStats stats = TankStatsUseCase.Compute(tank, _loadout);
            return new HangarTankStats(stats.Health, stats.Speed, stats.CannonDamage, stats.CannonReload);
        }

        /// <summary>
        /// The tank's saved loadout in ModuleSlot order, empty slots left out; with a replacement, its slot holds the
        /// replacement at its owned level, or 1 when not owned (the shop's "before → after").
        /// </summary>
        public static void FillLoadout(IMetaProgress meta, TankId tank, ModuleConfig replacement, List<ModuleLevel> loadout)
        {
            loadout.Clear();
            for (int slot = 0; slot < TankConfig.SLOT_COUNT; slot++)
            {
                ModuleId id;
                int level;
                if (replacement != null && (int)replacement.Slot == slot)
                {
                    id = replacement.Id;
                    level = Mathf.Max(1, meta.GetModuleLevel(id));
                }
                else
                {
                    id = meta.GetInstalled(tank, (ModuleSlot)slot);
                    level = id.IsNone ? 0 : meta.GetModuleLevel(id);
                }

                if (!id.IsNone)
                    loadout.Add(new ModuleLevel(id, level));
            }
        }

        public PurchaseResult CanBuy(string moduleId) =>
            FindModule(moduleId) != null ? _meta.CanBuyModule(new ModuleId(moduleId)) : PurchaseResult.Unknown;

        public PurchaseResult CanUpgrade(string moduleId) =>
            FindModule(moduleId) != null ? _meta.CanUpgradeModule(new ModuleId(moduleId)) : PurchaseResult.Unknown;

        public PurchaseResult TryBuy(string moduleId) =>
            FindModule(moduleId) != null ? _meta.BuyModule(new ModuleId(moduleId)) : PurchaseResult.Unknown;

        public PurchaseResult TryUpgrade(string moduleId) =>
            FindModule(moduleId) != null ? _meta.UpgradeModule(new ModuleId(moduleId)) : PurchaseResult.Unknown;

        public PurchaseResult TryInstall(string moduleId)
        {
            ModuleConfig module = FindModule(moduleId);
            if (module == null)
                return PurchaseResult.Unknown;
            if (!_meta.OwnsModule(module.Id))
                return PurchaseResult.NotOwned;

            TankId tank = _meta.SelectedTank;
            if (_meta.GetInstalled(tank, module.Slot) == module.Id)
                return PurchaseResult.Done;

            // Owned, so a refusal is something else (the tank, the slot): NotOwned would lie.
            return _meta.Install(tank, module.Slot, module.Id) ? PurchaseResult.Done : PurchaseResult.Unknown;
        }

        public void StartChapter(string chapterId)
        {
            ChapterConfig chapter = string.IsNullOrEmpty(chapterId) || _catalog == null
                ? null
                : _catalog.FindChapter(new ChapterId(chapterId));
            if (chapter == null)
            {
                Debug.LogWarning($"{nameof(MetaProgressHangarSource)}: no chapter '{chapterId}' in the catalog");
                return;
            }

            if (!IsUnlocked(chapter, _meta.GetChapter(chapter.Id)))
            {
                Debug.LogWarning($"{nameof(MetaProgressHangarSource)}: chapter '{chapterId}' is locked");
                return;
            }

            // TODO core: StartChapter. IGameFlow and GameFlow have no StartChapter(ChapterId) yet, so the run does
            // not learn the chapter: start the chapter's level, else the level catalog's first level.
            IGameFlow flow = _flow ?? AppServices.GameFlow;
            LevelConfig level = chapter.Level;
            if (level == null && flow is GameFlow coreFlow && coreFlow.Catalog != null)
                level = coreFlow.Catalog.FirstLevel;
            if (level == null)
            {
                Debug.LogWarning($"{nameof(MetaProgressHangarSource)}: chapter '{chapterId}' has no level");
                return;
            }

            _meta.Save(); // GDD §14: saved on leaving the menu
            flow.StartLevel(level.Id);
        }

        // ---------- Modules ----------

        private HangarModule Build(ModuleConfig module)
        {
            string id = module.Id.Value;
            int level = _meta.GetModuleLevel(module.Id);
            bool owned = level > 0;
            bool locked = !owned && _meta.CanBuyModule(module.Id) == PurchaseResult.Locked;
            int upgradeMarks = -1;
            int upgradeSamples = -1;
            if (owned && level < module.LevelCap)
            {
                ModuleConfig.Cost cost = module.UpgradeCost(level + 1);
                upgradeMarks = cost.marks;
                upgradeSamples = cost.samples;
            }

            // ModuleConfig.UnlockHint is a designer's free text, not a UiStrings key: the UI keeps its own
            // lock texts in the string table under module.<id>.lock.
            return new HangarModule(id, HangarSlots.Id(module.Slot), owned, locked,
                locked ? "module." + id + ".lock" : null, level, module.LevelCap, module.PriceMarks,
                module.PriceSamples, upgradeMarks, upgradeSamples);
        }

        private ModuleConfig FindModule(string moduleId) =>
            string.IsNullOrEmpty(moduleId) || _catalog == null ? null : _catalog.FindModule(new ModuleId(moduleId));

        private TankConfig FindTank()
        {
            if (_catalog == null)
                return null;
            TankConfig tank = _catalog.FindTank(_meta.SelectedTank);
            return tank != null || _catalog.Tanks.Count == 0 ? tank : _catalog.Tanks[0];
        }

        // ---------- Lists ----------

        private void EnsureLists()
        {
            if (!_listsDirty)
                return;

            _listsDirty = false;
            for (int i = 0; i < _slotModules.Length; i++)
                _slotModules[i].Clear();
            IReadOnlyList<ModuleConfig> modules = CatalogModules;
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleConfig module = modules[i];
                if (module != null && (int)module.Slot < _slotModules.Length)
                    _slotModules[(int)module.Slot].Add(Build(module));
            }

            _chapters.Clear();
            IReadOnlyList<ChapterConfig> chapters = CatalogChapters;
            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterConfig chapter = chapters[i];
                if (chapter == null)
                    continue;

                ChapterProgress progress = _meta.GetChapter(chapter.Id);
                _chapters.Add(new HangarChapter(chapter.Id.Value, IsUnlocked(chapter, progress), progress.Completed,
                    progress.BestArena, chapter.ArenaCount, progress.BestTimeSec, chapter.FirstCompletionMarks,
                    chapter.FirstCompletionSamples, chapter.RecommendedTank.IsNone ? null : chapter.RecommendedTank.Value));
            }
        }

        /// <summary>The save's flag; a chapter with no required one is open from the start (as the core's NewGame sets it).</summary>
        private static bool IsUnlocked(ChapterConfig chapter, ChapterProgress progress) =>
            progress.Unlocked || progress.Completed || chapter.RequiredChapter == null;

        private void OnMetaChanged()
        {
            _listsDirty = true;
            Changed?.Invoke();
        }
    }
}
