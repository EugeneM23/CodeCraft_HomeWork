using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// One row or card of the hangar's lists (slots, modules, upgrades, shop cards, chapters): the parts the menu
    /// builder gave it, any of them optional. No logic: screens clone a switched-off template and write the texts.
    /// </summary>
    public sealed class HangarItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [Tooltip("Selected look, shown on the selected item")]
        [SerializeField] private GameObject _marker;
        [Tooltip("The slot's or module's icon inside the icon frame")]
        [SerializeField] private Image _icon;
        [Tooltip("Short text shown when there is no icon")]
        [SerializeField] private TMP_Text _glyph;
        [Tooltip("Icons by slot and module id; without it (or without an icon) the glyph shows")]
        [SerializeField] private UiIconSetConfig _icons;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _info;
        [SerializeField] private TMP_Text _extra;
        [SerializeField] private TMP_Text _price;
        [Tooltip("The item's own action (Upgrade, Buy)")]
        [SerializeField] private Button _action;
        [SerializeField] private TMP_Text _actionLabel;

        public Button Button => _button;
        public TMP_Text Title => _title;
        public TMP_Text Info => _info;
        public TMP_Text Extra => _extra;
        public TMP_Text Price => _price;
        public Button Action => _action;
        public TMP_Text ActionLabel => _actionLabel;

        public void SetSelected(bool selected)
        {
            if (_marker != null)
                _marker.SetActive(selected);
        }

        /// <summary>The module's icon (its slot's when it has none), or the slot's glyph; tinted by the slot's colour.</summary>
        public void SetIcon(string slotId, string moduleId = null)
        {
            if (_icon == null && _glyph == null)
                return;
            Sprite sprite = _icons != null ? _icons.ModuleOrSlot(moduleId, slotId) : null;
            UiIconSetConfig.Show(_icon, _glyph, sprite, HangarLook.SlotGlyph(slotId),
                UiIconSetConfig.Tint(HangarLook.SlotColor(slotId)));
        }

        /// <summary>A switched-on copy of this (switched-off) template next to it.</summary>
        public HangarItemView Clone(string name)
        {
            gameObject.SetActive(false);
            HangarItemView copy = Instantiate(this, transform.parent);
            copy.name = name;
            copy.gameObject.SetActive(true);
            return copy;
        }
    }
}
