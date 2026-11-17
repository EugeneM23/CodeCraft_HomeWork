using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Saving.UI
{
    /// <summary>
    /// A ready save/load menu (the SaveSlotsMenu prefab): a list of slots with name, time, play time and scene; in Save
    /// mode picking a slot saves into it (a taken one asks first), in Load mode loads it; Delete asks too, the name
    /// field renames. Fixed slots (Slot Count &gt; 0) or every saved slot plus a "New slot" row (0). Buttons open it:
    /// OpenSave, OpenLoad, Close, Toggle. Works on the save system set here or SaveLoadSystem.Current, on unscaled time,
    /// with mouse, keyboard and gamepad (the scene needs an EventSystem). Pausing the game is up to the game.
    /// </summary>
    [AddComponentMenu("Save System/Save Slots Menu")]
    public sealed class SaveSlotsMenu : MonoBehaviour
    {
        /// <summary>Every text the menu writes, to translate or restyle.</summary>
        [Serializable]
        public sealed class Texts
        {
            public string SaveTitle = "Save Game";
            public string LoadTitle = "Load Game";
            public string Empty = "Empty";
            public string NewSlot = "New slot";
            public string Save = "Save";
            public string Overwrite = "Overwrite";
            public string Load = "Load";
            public string AskOverwrite = "Overwrite {0}?";
            public string AskDelete = "Delete {0}?";
            public string AskLoad = "Load {0}? Unsaved progress is lost.";
        }

        [Tooltip("Empty: the save system in play (SaveLoadSystem.Current)")]
        [SerializeField] private SaveLoadSystem _saves;
        [SerializeField] private SaveSlotsMenuMode _mode = SaveSlotsMenuMode.Load;
        [Tooltip("The first slot the menu shows")]
        [SerializeField] private int _firstSlot;
        [Tooltip("How many slots to show; 0 shows every saved slot and, to save, a New slot row")]
        [SerializeField] private int _slotCount = 5;
        [SerializeField] private bool _closeAfterAction = true;
        [SerializeField] private bool _confirmOverwrite = true;
        [SerializeField] private bool _confirmDelete = true;
        [SerializeField] private bool _confirmLoad;
        [SerializeField] private Texts _texts = new();

        [Header("Parts")]
        [Tooltip("Shown while the menu is open")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _list;
        [Tooltip("An inactive row inside the list: copied for each slot")]
        [SerializeField] private SaveSlotView _slotTemplate;
        [SerializeField] private Button _closeButton;
        [Tooltip("Optional: asks before overwriting, deleting or loading")]
        [SerializeField] private SaveConfirmDialog _confirm;

        [SerializeField] private UnityEvent _onOpened = new();
        [SerializeField] private UnityEvent _onClosed = new();

        private readonly List<SaveSlotView> _rows = new();
        private ISaveLoadService _listened;
        private bool _wired;

        public bool IsOpen => _panel != null && _panel.activeSelf;
        public SaveSlotsMenuMode Mode => _mode;
        public IReadOnlyList<SaveSlotView> Rows => _rows;
        public SaveConfirmDialog Confirm => _confirm;
        public UnityEvent OnOpened => _onOpened;
        public UnityEvent OnClosed => _onClosed;

        /// <summary>The save system it works on: the one set (unless it gave way to another), or SaveLoadSystem.Current.</summary>
        public ISaveLoadService Saves =>
            _savesOverride ?? (_saves != null && !_saves.IsIdle ? _saves : SaveLoadSystem.Current);

        private ISaveLoadService _savesOverride;

        /// <summary>Works on this service instead (tests, code that does not use SaveLoadSystem).</summary>
        public void Use(ISaveLoadService saves) => _savesOverride = saves;

        /// <summary>Shows the slots to save into, the first one selected.</summary>
        public void OpenSave() => Open(SaveSlotsMenuMode.Save);

        /// <summary>Shows the slots to load, the first saved one selected.</summary>
        public void OpenLoad() => Open(SaveSlotsMenuMode.Load);

        /// <summary>Opens in the mode set in the Inspector (or the last one).</summary>
        public void Open() => Open(_mode);

        public void Open(SaveSlotsMenuMode mode)
        {
            Wire();
            _mode = mode;
            if (_panel != null)
                _panel.SetActive(true);

            if (_confirm != null)
                _confirm.Hide();

            Listen(true);
            Refresh();
            SelectFirst();
            if (EventSystem.current == null)
                Debug.LogWarning("The slot menu needs an EventSystem in the scene to take clicks: " +
                                 "GameObject → UI → Event System.", this);

            _onOpened.Invoke();
        }

        public void Close()
        {
            if (_confirm != null)
                _confirm.Hide();

            bool wasOpen = IsOpen;
            if (_panel != null)
                _panel.SetActive(false);

            Listen(false);
            if (wasOpen)
                _onClosed.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>Rebuilds the rows from the slots on disk; the menu does it on its own when slots change.</summary>
        public void Refresh()
        {
            ISaveLoadService saves = Saves;
            var shown = new List<(int Slot, SlotInfo Info, bool IsNew)>();
            if (saves != null)
            {
                if (_slotCount > 0)
                {
                    for (int index = 0; index < _slotCount; index++)
                        shown.Add((_firstSlot + index, saves.GetSlot(_firstSlot + index), false));
                }
                else
                {
                    foreach (SlotInfo info in saves.GetSlots())
                    {
                        if (info.Slot >= _firstSlot)
                            shown.Add((info.Slot, info, false));
                    }

                    if (_mode == SaveSlotsMenuMode.Save)
                        shown.Add((saves.GetFreeSlot(_firstSlot), null, true));
                }
            }

            if (_title != null)
                _title.text = _mode == SaveSlotsMenuMode.Save ? _texts.SaveTitle : _texts.LoadTitle;

            for (int index = 0; index < shown.Count; index++)
            {
                SaveSlotView row = index < _rows.Count ? _rows[index] : AddRow();
                if (row == null)
                    break;

                row.gameObject.SetActive(true);
                row.Show(shown[index].Slot, shown[index].Info, _mode, _texts, shown[index].IsNew);
            }

            for (int index = shown.Count; index < _rows.Count; index++)
                _rows[index].gameObject.SetActive(false);
        }

        /// <summary>The row of a slot as shown now, or null.</summary>
        public SaveSlotView RowOf(int slot)
        {
            foreach (SaveSlotView row in _rows)
            {
                if (row.gameObject.activeSelf && row.Slot == slot)
                    return row;
            }

            return null;
        }

        // ---------- Rows ----------

        private void Pick(SaveSlotView row)
        {
            ISaveLoadService saves = Saves;
            if (saves == null)
                return;

            // The slot as asked about: rows are reused and may show another slot by the time the answer comes
            int slot = row.Slot;
            string title = SaveSlotFormat.Title(slot, row.Info);
            if (_mode == SaveSlotsMenuMode.Save)
            {
                if (!row.IsEmpty && _confirmOverwrite)
                    Ask(string.Format(_texts.AskOverwrite, title), () => SaveInto(slot));
                else
                    SaveInto(slot);
            }
            else if (!row.IsEmpty)
            {
                if (_confirmLoad)
                    Ask(string.Format(_texts.AskLoad, title), () => LoadFrom(slot));
                else
                    LoadFrom(slot);
            }
        }

        private void SaveInto(int slot)
        {
            ISaveLoadService saves = Saves;
            if (saves.IsLoading)
                return;

            if (slot != saves.CurrentSlot)
                saves.SelectSlot(slot, keepData: true);

            saves.Save();
            if (_closeAfterAction)
                Close();
            else
                Refresh();
        }

        private void LoadFrom(int slot)
        {
            ISaveLoadService saves = Saves;
            if (saves.IsLoading)
                return;

            // Closed first: a load may reload the scene this menu lives in
            if (_closeAfterAction)
                Close();

            saves.SelectSlot(slot);
            saves.Load();
        }

        private void Delete(SaveSlotView row)
        {
            int slot = row.Slot;
            if (_confirmDelete)
                Ask(string.Format(_texts.AskDelete, SaveSlotFormat.Title(slot, row.Info)), () => DeleteSlot(slot));
            else
                DeleteSlot(slot);
        }

        private void DeleteSlot(int slot)
        {
            Saves?.DeleteSlot(slot);
            Refresh();
            SelectFirst();
        }

        private void Rename(SaveSlotView row, string slotName)
        {
            if (!row.IsEmpty && row.Info != null && slotName != row.Info.Name)
                Saves?.RenameSlot(row.Slot, slotName);
        }

        private void Ask(string message, Action onYes)
        {
            if (_confirm != null)
                _confirm.Ask(message, onYes);
            else
                onYes();
        }

        private SaveSlotView AddRow()
        {
            if (_slotTemplate == null || _list == null)
            {
                Debug.LogError("The slot menu needs a slot template and a list to put rows in.", this);
                return null;
            }

            SaveSlotView row = Instantiate(_slotTemplate, _list);
            row.name = $"Slot {_rows.Count}";
            row.Picked += Pick;
            row.DeleteClicked += Delete;
            row.Renamed += Rename;
            _rows.Add(row);
            return row;
        }

        private void SelectFirst()
        {
            if (EventSystem.current == null)
                return;

            foreach (SaveSlotView row in _rows)
            {
                if (row.gameObject.activeSelf && row.PickButton != null && row.PickButton.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(row.PickButton.gameObject);
                    return;
                }
            }

            if (_closeButton != null)
                EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
        }

        // ---------- Wiring ----------

        private void Wire()
        {
            if (_wired)
                return;

            _wired = true;
            if (_slotTemplate != null)
                _slotTemplate.gameObject.SetActive(false);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void Listen(bool on)
        {
            if (_listened != null)
            {
                _listened.SlotsChanged -= OnSlotsChanged;
                _listened = null;
            }

            if (!on || Saves == null)
                return;

            _listened = Saves;
            _listened.SlotsChanged += OnSlotsChanged;
        }

        private void OnSlotsChanged()
        {
            if (IsOpen)
                Refresh();
        }

        private void Awake()
        {
            Wire();
            if (_panel != null && _panel != gameObject)
                _panel.SetActive(false);
        }

        private void OnDestroy() => Listen(false);

        // Instead of [Min]: the old Odin in the project breaks drawing [Min] in Unity 6.5
        private void OnValidate()
        {
            _firstSlot = Mathf.Max(0, _firstSlot);
            _slotCount = Mathf.Max(0, _slotCount);
        }
    }
}
