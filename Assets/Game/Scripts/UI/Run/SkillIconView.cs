using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// One place of a pooled row of run skills (HUD icons, the pause list): the skill's icon from the icon set in a
    /// frame (the two-letter glyph when it has none), its level, and optionally its name. Rewrites itself only
    /// when the skill or its level changed.
    /// </summary>
    public sealed class SkillIconView : MonoBehaviour
    {
        [Tooltip("The skill's icon inside the frame")]
        [SerializeField] private Image _symbol;
        [SerializeField] private TMP_Text _glyph;
        [SerializeField] private TMP_Text _level;
        [Tooltip("Optional: the skill's name (pause list)")]
        [SerializeField] private TMP_Text _name;
        [Tooltip("Icons by skill id; without it (or without an icon) the glyph shows")]
        [SerializeField] private UiIconSetConfig _icons;

        private string _shownId;
        private int _shownLevel = -1;

        public void Show(RunSkill skill)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (skill.Id != _shownId)
            {
                _shownId = skill.Id;
                Sprite sprite = _icons != null ? _icons.Skill(skill.Id) : null;
                UiIconSetConfig.Show(_symbol, _glyph, sprite, UiStrings.Get(SkillVisuals.GlyphKey(skill.Id)),
                    UiIconSetConfig.Tint(SkillVisuals.ColorOf(skill.Id)));
                if (_name != null)
                    _name.text = UiStrings.Get(SkillVisuals.NameKey(skill.Id));
            }

            if (skill.Level != _shownLevel)
            {
                _shownLevel = skill.Level;
                _level.SetText("{0:0}", skill.Level);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
