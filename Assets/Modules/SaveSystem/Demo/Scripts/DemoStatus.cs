using Saving.UI;
using TMPro;
using UnityEngine;

namespace Saving.Demo
{
    /// <summary>A line about the save system in play: current slot, its name, play time and the last save or load.</summary>
    public sealed class DemoStatus : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private string _last = "-";
        private SaveLoadSystem _listened;

        private void Update()
        {
            SaveLoadSystem saves = SaveLoadSystem.Current;
            if (saves != _listened)
                Listen(saves);

            if (_label == null)
                return;

            _label.text = saves == null
                ? "No save system in play"
                : $"Slot {saves.CurrentSlot + 1}{(string.IsNullOrEmpty(saves.SlotName) ? "" : $" \"{saves.SlotName}\"")}" +
                  $"   played {SaveSlotFormat.PlayTime(saves.PlayTime)}   last {_last}";
        }

        private void OnDestroy() => Listen(null);

        private void Listen(SaveLoadSystem saves)
        {
            if (_listened != null)
            {
                _listened.Saved -= OnSaved;
                _listened.Loaded -= OnLoaded;
            }

            _listened = saves;
            if (saves == null)
                return;

            saves.Saved += OnSaved;
            saves.Loaded += OnLoaded;
        }

        private void OnSaved(SaveOutcome outcome) => _last = $"save {outcome}";
        private void OnLoaded(LoadOutcome outcome) => _last = $"load {outcome}";
    }
}
