using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// One card of the skill choice: icon (coloured square and glyph), name, kind (weapon or upgrade), level
    /// «2 → 3» or «новый», the description of the level it goes to, and the key that picks it. A button: the
    /// click goes up as Clicked with the card's index.
    /// </summary>
    public sealed class SkillCardView : MonoBehaviour
    {
        public event Action<int> Clicked;

        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _glyph;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _kind;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _key;

        private int _index;

        public Button Button => _button;

        /// <summary>Fills the card (once per offer, not per frame).</summary>
        public void Setup(SkillOffer offer, int index, string keyName)
        {
            _index = index;
            gameObject.SetActive(true);

            _icon.color = SkillVisuals.ColorOf(offer.Id);
            _glyph.text = UiStrings.Get(SkillVisuals.GlyphKey(offer.Id));
            _name.text = UiStrings.Get(SkillVisuals.NameKey(offer.Id));
            _kind.text = UiStrings.Get(offer.IsWeapon ? "run.skill_weapon" : "run.skill_upgrade");
            _description.text = UiStrings.Get(SkillVisuals.DescriptionKey(offer.Id, offer.ToLevel));

            if (offer.FromLevel <= 0)
            {
                _level.text = UiStrings.Get("run.choice_new");
                _level.color = UiStyle.Good;
            }
            else
            {
                _level.text = string.Format(UiStrings.Get("run.choice_level_format"), offer.FromLevel, offer.ToLevel);
                _level.color = UiStyle.Accent;
            }

            _key.text = string.Format(UiStrings.Get("run.choice_key_format"), keyName);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnEnable() => _button.onClick.AddListener(HandleClick);
        private void OnDisable() => _button.onClick.RemoveListener(HandleClick);

        private void HandleClick() => Clicked?.Invoke(_index);
    }
}
