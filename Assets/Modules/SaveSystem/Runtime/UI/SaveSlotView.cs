using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Saving.UI
{
    /// <summary>
    /// One slot of the menu: title (name or "Slot N"), details (time, play time, scene), the pick button (Load, Save or
    /// Overwrite), Delete and an optional name field. It only shows a slot and reports clicks; the menu decides.
    /// </summary>
    public sealed class SaveSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _details;
        [SerializeField] private Button _pick;
        [SerializeField] private TMP_Text _pickLabel;
        [SerializeField] private Button _delete;
        [Tooltip("Optional: renames the slot when editing ends")]
        [SerializeField] private TMP_InputField _name;

        private bool _wired;

        public event Action<SaveSlotView> Picked;
        public event Action<SaveSlotView> DeleteClicked;
        public event Action<SaveSlotView, string> Renamed;

        public int Slot { get; private set; }
        public bool IsEmpty { get; private set; }
        public SlotInfo Info { get; private set; }
        public Button PickButton => _pick;
        public Button DeleteButton => _delete;
        public TMP_InputField NameField => _name;
        public string Title => _title != null ? _title.text : string.Empty;
        public string Details => _details != null ? _details.text : string.Empty;
        public string PickLabel => _pickLabel != null ? _pickLabel.text : string.Empty;

        /// <summary>Shows the slot (info null: empty; isNew: the "new slot" row) for the mode; texts come from the menu.</summary>
        public void Show(int slot, SlotInfo info, SaveSlotsMenuMode mode, SaveSlotsMenu.Texts texts, bool isNew = false)
        {
            Wire();
            Slot = slot;
            Info = info;
            IsEmpty = info == null;

            SetText(_title, isNew ? texts.NewSlot : SaveSlotFormat.Title(slot, info));
            SetText(_details, IsEmpty ? texts.Empty : SaveSlotFormat.Details(info));
            SetText(_pickLabel, mode == SaveSlotsMenuMode.Load ? texts.Load : IsEmpty ? texts.Save : texts.Overwrite);

            if (_pick != null)
                _pick.interactable = mode == SaveSlotsMenuMode.Save || !IsEmpty;

            if (_delete != null)
                _delete.gameObject.SetActive(!IsEmpty);

            if (_name != null)
            {
                _name.gameObject.SetActive(!IsEmpty);
                _name.SetTextWithoutNotify(info?.Name ?? string.Empty);
            }
        }

        private void Wire()
        {
            if (_wired)
                return;

            _wired = true;
            if (_pick != null)
                _pick.onClick.AddListener(() => Picked?.Invoke(this));

            if (_delete != null)
                _delete.onClick.AddListener(() => DeleteClicked?.Invoke(this));

            if (_name != null)
                _name.onEndEdit.AddListener(text => Renamed?.Invoke(this, text));
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
