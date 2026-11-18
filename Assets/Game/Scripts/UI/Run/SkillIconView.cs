using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// One place of a pooled row of run skills (HUD icons, the pause list): a coloured square with the skill's
    /// glyph, its level, and optionally its name. Rewrites itself only when the skill or its level changed.
    /// </summary>
    public sealed class SkillIconView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _glyph;
        [SerializeField] private TMP_Text _level;
        [Tooltip("Optional: the skill's name (pause list)")]
        [SerializeField] private TMP_Text _name;

        private string _shownId;
        private int _shownLevel = -1;

        public void Show(RunSkill skill)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (skill.Id != _shownId)
            {
                _shownId = skill.Id;
                _background.color = SkillVisuals.ColorOf(skill.Id);
                _glyph.text = UiStrings.Get(SkillVisuals.GlyphKey(skill.Id));
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
