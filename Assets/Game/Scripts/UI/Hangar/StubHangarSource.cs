using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB IHangarSource until the core's save (GDD §14, IMetaProgress) and its catalog asset: reads the core's
    /// catalog types (GameCatalog, ModuleConfig, TankConfig, ChapterConfig) from the placeholder HangarStubConfig in
    /// Resources and keeps the progress in persistentDataPath/progress_m1.json (JsonUtility). Every change saves the
    /// file and raises Changed. Standard modules are owned and installed from the start. The numbers the hangar
    /// shows come from HangarStubConfig.moduleStats, not from the combat team. STUB: a chapter starts its
    /// ChapterConfig.Level, which the placeholder builder sets to the core level catalog's first level.
    /// </summary>
    public sealed class StubHangarSource : IHangarSource
    {
        public const string FILE_NAME = "progress_m1.json";
        private const int VERSION = 1;

        public event Action Changed;

        private readonly HangarStubConfig _config;
        private readonly GameCatalog _catalog;
        private readonly string _path;
        private readonly IGameFlow _flow;
        private readonly List<HangarModule>[] _slotModules = new List<HangarModule>[HangarSlots.Ids.Length];
        private readonly List<HangarChapter> _chapters = new();
        private readonly List<HangarModule> _none = new();
        private HangarSaveData _data;
        private bool _listsDirty = true;

        /// <summary>The app's stub: HangarStubConfig from Resources, the file in persistentDataPath, AppServices.GameFlow.</summary>
        public StubHangarSource() : this(LoadConfig(), Path.Combine(Application.persistentDataPath, FILE_NAME), null)
        {
        }

        /// <summary>For tests: the config, the save file's full path and the flow StartChapter calls (null: AppServices.GameFlow).</summary>
        public StubHangarSource(HangarStubConfig config, string savePath, IGameFlow flow)
        {
            _config = config;
            _catalog = config != null ? config.catalog : null;
            _path = savePath;
            _flow = flow;
            for (int i = 0; i < _slotModules.Length; i++)
                _slotModules[i] = new List<HangarModule>();

            _data = Read() ?? NewData();
            Normalize();
        }

        public int Marks => _data.marks;
        public int Samples => _data.samples;
        public bool HasSave => !string.IsNullOrEmpty(_path) && File.Exists(_path);
        public string SelectedTankId => _data.selectedTank;
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

        /// <summary>Wipes the save file and starts a new game, saved at once (Continue appears in the main menu).</summary>
        public void NewGame()
        {
            if (HasSave)
                File.Delete(_path);
            _data = NewData();
            Normalize();
            SaveAndRaise();
        }

        public string InstalledIn(string slotId)
        {
            HangarLoadoutSave entry = FindLoadout(slotId);
            return entry != null && !string.IsNullOrEmpty(entry.module) ? entry.module : null;
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
            HangarTankStats baseStats = new(tank != null ? tank.Health : 1000f, tank != null ? tank.Speed : 6f,
                _config != null ? _config.cannonDamage : 0f, _config != null ? _config.cannonReload : 0f);

            ModuleConfig replacement = FindModule(replacementModuleId);
            HangarTankStats bonus = default;
            HangarTankStats percent = default;
            for (int i = 0; i < HangarSlots.Ids.Length; i++)
            {
                string id = replacement != null && (int)replacement.Slot == i
                    ? replacement.Id.Value
                    : InstalledIn(HangarSlots.Ids[i]);
                if (id == null || _config == null)
                    continue;

                HangarStatsUseCase.Add(_config.FindStats(id), Mathf.Max(1, LevelOf(id)), ref bonus, ref percent);
            }

            return HangarStatsUseCase.Finish(baseStats, bonus, percent);
        }

        public PurchaseResult CanBuy(string moduleId)
        {
            ModuleConfig module = FindModule(moduleId);
            if (module == null)
                return PurchaseResult.Unknown;
            if (LevelOf(moduleId) > 0)
                return PurchaseResult.AlreadyOwned;
            if (IsLocked(module))
                return PurchaseResult.Locked;
            return CanPay(module.PriceMarks, module.PriceSamples);
        }

        public PurchaseResult CanUpgrade(string moduleId)
        {
            ModuleConfig module = FindModule(moduleId);
            if (module == null)
                return PurchaseResult.Unknown;

            int level = LevelOf(moduleId);
            if (level < 1)
                return PurchaseResult.NotOwned;
            if (level >= module.LevelCap)
                return PurchaseResult.MaxLevel;

            ModuleConfig.Cost cost = module.UpgradeCost(level + 1);
            return CanPay(cost.marks, cost.samples);
        }

        public PurchaseResult TryBuy(string moduleId)
        {
            PurchaseResult result = CanBuy(moduleId);
            if (result != PurchaseResult.Done)
                return result;

            ModuleConfig module = FindModule(moduleId);
            _data.marks -= module.PriceMarks;
            _data.samples -= module.PriceSamples;
            _data.modules.Add(new HangarModuleSave { id = moduleId, level = 1 });
            string slot = HangarSlots.Id(module.Slot);
            if (InstalledIn(slot) == null)
                SetInstalled(slot, moduleId);
            SaveAndRaise();
            return PurchaseResult.Done;
        }

        public PurchaseResult TryInstall(string moduleId)
        {
            ModuleConfig module = FindModule(moduleId);
            if (module == null)
                return PurchaseResult.Unknown;
            if (LevelOf(moduleId) < 1)
                return PurchaseResult.NotOwned;

            string slot = HangarSlots.Id(module.Slot);
            if (InstalledIn(slot) == moduleId)
                return PurchaseResult.Done;

            SetInstalled(slot, moduleId);
            SaveAndRaise();
            return PurchaseResult.Done;
        }

        public PurchaseResult TryUpgrade(string moduleId)
        {
            PurchaseResult result = CanUpgrade(moduleId);
            if (result != PurchaseResult.Done)
                return result;

            HangarModuleSave owned = FindOwned(moduleId);
            ModuleConfig.Cost cost = FindModule(moduleId).UpgradeCost(owned.level + 1);
            _data.marks -= cost.marks;
            _data.samples -= cost.samples;
            owned.level++;
            SaveAndRaise();
            return PurchaseResult.Done;
        }

        public void StartChapter(string chapterId)
        {
            IReadOnlyList<HangarChapter> chapters = Chapters;
            IReadOnlyList<ChapterConfig> configs = CatalogChapters;
            for (int i = 0; i < chapters.Count && i < configs.Count; i++)
            {
                if (chapters[i].Id != chapterId)
                    continue;

                LevelConfig level = configs[i] != null ? configs[i].Level : null;
                if (!chapters[i].Unlocked || level == null)
                {
                    Debug.LogWarning($"{nameof(StubHangarSource)}: chapter '{chapterId}' is locked or has no level");
                    return;
                }

                Save(); // GDD §14: saved on leaving the menu
                (_flow ?? AppServices.GameFlow).StartLevel(level.Id);
                return;
            }
        }

        // ---------- Modules ----------

        private HangarModule Build(ModuleConfig module)
        {
            string id = module.Id.Value;
            int level = LevelOf(id);
            bool owned = level > 0;
            bool locked = !owned && IsLocked(module);
            int upgradeMarks = -1;
            int upgradeSamples = -1;
            if (owned && level < module.LevelCap)
            {
                ModuleConfig.Cost cost = module.UpgradeCost(level + 1);
                upgradeMarks = cost.marks;
                upgradeSamples = cost.samples;
            }

            return new HangarModule(id, HangarSlots.Id(module.Slot), owned, locked,
                locked ? "module." + id + ".lock" : null, level, module.LevelCap, module.PriceMarks,
                module.PriceSamples, upgradeMarks, upgradeSamples);
        }

        private static bool IsLocked(ModuleConfig module) => !module.IsStandard && !module.InShop;

        private PurchaseResult CanPay(int marks, int samples)
        {
            if (_data.marks < marks)
                return PurchaseResult.NotEnoughMarks;
            if (_data.samples < samples)
                return PurchaseResult.NotEnoughSamples;
            return PurchaseResult.Done;
        }

        private int LevelOf(string moduleId)
        {
            HangarModuleSave owned = FindOwned(moduleId);
            return owned != null ? owned.level : 0;
        }

        private HangarModuleSave FindOwned(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId))
                return null;
            for (int i = 0; i < _data.modules.Count; i++)
                if (_data.modules[i].id == moduleId)
                    return _data.modules[i];
            return null;
        }

        private ModuleConfig FindModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId))
                return null;
            IReadOnlyList<ModuleConfig> modules = CatalogModules;
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null && modules[i].Id.Value == moduleId)
                    return modules[i];
            return null;
        }

        private TankConfig FindTank()
        {
            if (_catalog == null)
                return null;
            TankConfig tank = _catalog.FindTank(new TankId(_data.selectedTank));
            return tank != null || _catalog.Tanks.Count == 0 ? tank : _catalog.Tanks[0];
        }

        private HangarLoadoutSave FindLoadout(string slotId)
        {
            for (int i = 0; i < _data.loadout.Count; i++)
                if (_data.loadout[i].slot == slotId)
                    return _data.loadout[i];
            return null;
        }

        private void SetInstalled(string slotId, string moduleId)
        {
            HangarLoadoutSave entry = FindLoadout(slotId);
            if (entry == null)
                _data.loadout.Add(new HangarLoadoutSave { slot = slotId, module = moduleId });
            else
                entry.module = moduleId;
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

                string id = chapter.Id.Value;
                HangarChapterSave save = FindChapter(id);
                bool completed = save != null && save.completed;
                bool unlocked = completed || (save != null && save.unlocked) || chapter.RequiredChapter == null ||
                                IsCompleted(chapter.RequiredChapter.Id.Value);
                _chapters.Add(new HangarChapter(id, unlocked, completed, save != null ? save.bestArena : 0,
                    chapter.ArenaCount, save != null ? save.bestTime : 0f, chapter.FirstCompletionMarks,
                    chapter.FirstCompletionSamples, _config.RecommendedTank(i)));
            }
        }

        private HangarChapterSave FindChapter(string chapterId)
        {
            for (int i = 0; i < _data.chapters.Count; i++)
                if (_data.chapters[i].id == chapterId)
                    return _data.chapters[i];
            return null;
        }

        private bool IsCompleted(string chapterId)
        {
            HangarChapterSave save = FindChapter(chapterId);
            return save != null && save.completed;
        }

        // ---------- Save ----------

        private HangarSaveData NewData() => new()
        {
            version = VERSION,
            marks = _config != null ? _config.startMarks : 0,
            samples = _config != null ? _config.startSamples : 0,
            selectedTank = _config != null ? _config.startTank : TankIds.Tiger.Value
        };

        /// <summary>Standard modules owned, every slot filled with the tank's default or the slot's standard module.</summary>
        private void Normalize()
        {
            _data.modules ??= new List<HangarModuleSave>();
            _data.loadout ??= new List<HangarLoadoutSave>();
            _data.chapters ??= new List<HangarChapterSave>();
            if (string.IsNullOrEmpty(_data.selectedTank))
                _data.selectedTank = _config != null ? _config.startTank : TankIds.Tiger.Value;

            IReadOnlyList<ModuleConfig> modules = CatalogModules;
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleConfig module = modules[i];
                if (module != null && module.IsStandard && LevelOf(module.Id.Value) < 1)
                    _data.modules.Add(new HangarModuleSave { id = module.Id.Value, level = 1 });
            }

            TankConfig tank = FindTank();
            for (int i = 0; i < HangarSlots.Ids.Length; i++)
            {
                string slot = HangarSlots.Ids[i];
                string installed = InstalledIn(slot);
                if (installed != null && LevelOf(installed) > 0)
                    continue;

                string fallback = tank != null ? tank.DefaultModule((ModuleSlot)i).Value : null;
                if (string.IsNullOrEmpty(fallback) || LevelOf(fallback) < 1)
                    fallback = StandardOf((ModuleSlot)i);
                if (fallback != null)
                    SetInstalled(slot, fallback);
            }

            _listsDirty = true;
        }

        private string StandardOf(ModuleSlot slot)
        {
            IReadOnlyList<ModuleConfig> modules = CatalogModules;
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null && modules[i].IsStandard && modules[i].Slot == slot)
                    return modules[i].Id.Value;
            return null;
        }

        private HangarSaveData Read()
        {
            if (!HasSave)
                return null;

            try
            {
                return JsonUtility.FromJson<HangarSaveData>(File.ReadAllText(_path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{nameof(StubHangarSource)}: could not read {_path}, starting anew: {exception.Message}");
                return null;
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_path))
                return;

            _data.version = VERSION;
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(_data, true));
            if (File.Exists(_path))
                File.Delete(_path);
            File.Move(temp, _path);
        }

        private void SaveAndRaise()
        {
            Save();
            _listsDirty = true;
            Changed?.Invoke();
        }

        private static HangarStubConfig LoadConfig()
        {
            HangarStubConfig config = Resources.Load<HangarStubConfig>(HangarStubConfig.RESOURCE_PATH);
            if (config == null)
                Debug.LogWarning($"{nameof(StubHangarSource)}: no Resources/{HangarStubConfig.RESOURCE_PATH}; " +
                                 "run Tools/UI/Build Placeholder Data. The hangar is empty.");
            return config;
        }
    }
}
