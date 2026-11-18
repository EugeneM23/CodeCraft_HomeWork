using System;
using System.Collections.Generic;

namespace Game.Tests
{
    /// <summary>
    /// IMetaProgress for the hangar adapter's tests: levels, loadout and chapters in dictionaries; the buying calls
    /// answer what the test sets (Done applies the change and raises Changed), and every call is counted.
    /// </summary>
    public sealed class FakeMetaProgress : IMetaProgress
    {
        public event Action Changed;

        public readonly Dictionary<ModuleId, int> Levels = new();
        public readonly Dictionary<(TankId, ModuleSlot), ModuleId> Installed = new();
        public readonly Dictionary<ChapterId, ChapterProgress> Chapters = new();
        public readonly HashSet<ModuleId> LockedModules = new();

        public PurchaseResult BuyResult = PurchaseResult.Done;
        public PurchaseResult UpgradeResult = PurchaseResult.Done;
        public bool InstallFails;
        public int BuyCalls;
        public int UpgradeCalls;
        public int InstallCalls;
        public int SaveCalls;
        public int NewGameCalls;

        public bool HasSave { get; set; }
        public int Marks { get; set; }
        public int Samples { get; set; }
        public TankId SelectedTank { get; set; } = TankIds.Tiger;
        public ProgressStats Stats => default;

        public bool OwnsTank(TankId tank) => tank == SelectedTank;
        public bool OwnsModule(ModuleId module) => GetModuleLevel(module) > 0;
        public int GetModuleLevel(ModuleId module) => Levels.TryGetValue(module, out int level) ? level : 0;

        public ModuleId GetInstalled(TankId tank, ModuleSlot slot) =>
            Installed.TryGetValue((tank, slot), out ModuleId module) ? module : default;

        public ChapterProgress GetChapter(ChapterId chapter) =>
            Chapters.TryGetValue(chapter, out ChapterProgress progress) ? progress : default;

        public bool HasTutorialFlag(string flag) => false;

        public PurchaseResult CanBuyTank(TankId tank) => PurchaseResult.Unknown;

        public PurchaseResult CanBuyModule(ModuleId module)
        {
            if (OwnsModule(module))
                return PurchaseResult.AlreadyOwned;
            return LockedModules.Contains(module) ? PurchaseResult.Locked : BuyResult;
        }

        public PurchaseResult CanUpgradeModule(ModuleId module) =>
            OwnsModule(module) ? UpgradeResult : PurchaseResult.NotOwned;

        public PurchaseResult BuyTank(TankId tank) => PurchaseResult.Unknown;

        public PurchaseResult BuyModule(ModuleId module)
        {
            BuyCalls++;
            PurchaseResult result = CanBuyModule(module);
            if (result == PurchaseResult.Done)
            {
                Levels[module] = 1;
                Changed?.Invoke();
            }
            return result;
        }

        public PurchaseResult UpgradeModule(ModuleId module)
        {
            UpgradeCalls++;
            PurchaseResult result = CanUpgradeModule(module);
            if (result == PurchaseResult.Done)
            {
                Levels[module] = GetModuleLevel(module) + 1;
                Changed?.Invoke();
            }
            return result;
        }

        public bool SelectTank(TankId tank) => tank == SelectedTank;

        public bool Install(TankId tank, ModuleSlot slot, ModuleId module)
        {
            InstallCalls++;
            if (InstallFails || !OwnsModule(module))
                return false;
            Installed[(tank, slot)] = module;
            Changed?.Invoke();
            return true;
        }

        public void SetTutorialFlag(string flag)
        {
        }

        public void ApplyRun(RunResult result)
        {
        }

        public void Save() => SaveCalls++;

        public void NewGame()
        {
            NewGameCalls++;
            HasSave = true;
            Changed?.Invoke();
        }
    }
}
