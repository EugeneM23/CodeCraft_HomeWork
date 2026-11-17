using System;
using System.Collections.Generic;

namespace Saving
{
    /// <summary>The save system as game code sees it: the key for ServiceLocator.</summary>
    public interface ISaveLoadService
    {
        int CurrentSlot { get; }
        bool IsLoading { get; }
        bool IsReadOnly { get; }                     // slot from a newer game or failed migration: never overwritten
        double PlayTime { get; }
        IDictionary<string, string> Summary { get; } // written to meta.json
        string SlotName { get; set; }                // the current slot's name, written to meta.json
        SaveValues Values { get; }                   // values of the current slot under string keys

        event Action<SaveOutcome> Saved;
        event Action<LoadOutcome> Loaded;            // after the saved scene is restored too
        event Action SlotsChanged;                   // a slot was written, renamed, copied, imported or deleted

        void Register(IGameSerializer serializer);   // applies its section from the snapshot right away, if present
        void Unregister(IGameSerializer serializer); // does not capture
        void Unregister(string key);
        void Capture();                              // registered serializers -> snapshot, no disk write
        SaveOutcome Save(bool onlyIfChanged = false);
        LoadOutcome Load();                          // current slot: disk -> snapshot -> registered serializers
        void SelectSlot(int slot, bool keepData = false); // keepData: "save as" into another slot

        IReadOnlyList<SlotInfo> GetSlots();          // reads meta.json only
        SlotInfo GetSlot(int slot);                  // null when the slot has no meta.json
        bool HasSlot(int slot);                      // the slot has any file
        bool RenameSlot(int slot, string name);      // false when the slot is empty
        bool CopySlot(int from, int to);             // false when the source has nothing readable; into the current slot: Load next
        int GetLatestSlot();                         // the slot saved last, -1 when there is none
        int GetFreeSlot(int from = 0);               // the first slot from 'from' without any file
        void DeleteSlot(int slot);
        string ExportSlot(int slot);                 // the slot as one JSON string (a broken file gives way to its .bak), null when empty
        void ImportSlot(int slot, string package);   // writes an exported slot; into the current slot: Load next
    }
}
