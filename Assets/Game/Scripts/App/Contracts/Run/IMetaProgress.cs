using System;

namespace Game
{
    /// <summary>
    /// The player's progress between runs (GDD §9, §14): marks and samples, tanks, modules and their levels, each tank's
    /// loadout, chapters, tutorial flags and statistics. In the AppContext: <c>AppContext.Instance.GetMetaProgress()</c>.
    ///
    /// Every buying or changing call checks the catalog and the money, changes the progress and writes the save at once
    /// (GDD §14: after every purchase or upgrade); a refused one changes nothing and says why. A run's summary goes in
    /// through <see cref="ApplyRun"/> (the core calls it when a run ends), which also writes the save.
    /// The legacy IPlayerProgress keeps working over the same data until the menu moves to this one.
    /// </summary>
    public interface IMetaProgress
    {
        /// <summary>Anything changed: money, a purchase, a loadout, a chapter, a load.</summary>
        event Action Changed;

        int Marks { get; }

        int Samples { get; }

        TankId SelectedTank { get; }

        bool OwnsTank(TankId tank);

        bool OwnsModule(ModuleId module);

        /// <summary>0 — not owned; 1..3 otherwise.</summary>
        int GetModuleLevel(ModuleId module);

        /// <summary>The module in the tank's slot; the tank's default for a slot never set.</summary>
        ModuleId GetInstalled(TankId tank, ModuleSlot slot);

        ChapterProgress GetChapter(ChapterId chapter);

        bool HasTutorialFlag(string flag);

        ProgressStats Stats { get; }

        /// <summary>Whether the player can pay for the tank, the module or the next level now (and why not).</summary>
        PurchaseResult CanBuyTank(TankId tank);

        PurchaseResult CanBuyModule(ModuleId module);

        PurchaseResult CanUpgradeModule(ModuleId module);

        PurchaseResult BuyTank(TankId tank);

        PurchaseResult BuyModule(ModuleId module);

        PurchaseResult UpgradeModule(ModuleId module);

        /// <summary>Only an owned tank.</summary>
        bool SelectTank(TankId tank);

        /// <summary>Only an owned module of that slot into an owned tank; installing is free (GDD §8).</summary>
        bool Install(TankId tank, ModuleSlot slot, ModuleId module);

        void SetTutorialFlag(string flag);

        /// <summary>
        /// A run is over: its marks and samples are kept (GDD §3, also on a defeat), the chapter's best arena and time,
        /// completion and the next chapter's unlock, the statistics. Writes the save.
        /// </summary>
        void ApplyRun(RunResult result);

        /// <summary>Writes the save now (leaving to the menu, quitting).</summary>
        void Save();
    }
}
