using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// What the menus (main menu, hangar, slot assembly, upgrade, shop, chapter select) read and change in the
    /// player's progress. The UI's own narrow view: today a stub on placeholder data, after the core's
    /// contracts an adapter over the save (GDD §14) and the core's catalogs. Ids are the catalogs' ids; texts
    /// and icons come from the UI by id. Every change raises Changed and is saved by the source.
    /// </summary>
    public interface IHangarSource
    {
        event Action Changed;

        int Marks { get; }
        int Samples { get; }

        /// <summary>There is a game to continue (the main menu's Continue).</summary>
        bool HasSave { get; }

        /// <summary>Wipes the progress and starts from the beginning (after the player confirmed).</summary>
        void NewGame();

        string SelectedTankId { get; }

        /// <summary>The six slot ids in the hangar's order: gun, machine gun, armor, engine, tracks, special.</summary>
        IReadOnlyList<string> Slots { get; }

        /// <summary>Module installed in the slot of the selected tank; null when empty.</summary>
        string InstalledIn(string slotId);

        /// <summary>Every module of the slot: owned, for sale and locked.</summary>
        IReadOnlyList<HangarModule> ModulesFor(string slotId);

        HangarModule Module(string moduleId);

        /// <summary>The selected tank's numbers; with replacement, as if it were installed in its slot.</summary>
        TankStats Stats(string replacementModuleId = null);

        bool TryBuy(string moduleId);
        bool TryInstall(string moduleId);
        bool TryUpgrade(string moduleId);

        IReadOnlyList<HangarChapter> Chapters { get; }

        /// <summary>Starts a run of the chapter (the game flow loads it).</summary>
        void StartChapter(string chapterId);
    }
}
