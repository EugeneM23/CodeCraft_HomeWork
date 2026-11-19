using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Icons of the screens by id: run skills (SkillIds values), tank slots (ModuleSlot names) and modules (module
    /// ids; a module without its own icon shows its slot's). Built by the editor's UiIconSetBuilder from the UI
    /// pack's white icons and given to the views by the prefab builders. An id without a sprite keeps the glyph.
    /// Lookups are a short scan, done when a view's id changes, never per frame.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI/Icon Set", fileName = "UiIconSetConfig")]
    public sealed class UiIconSetConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> _skills = new();
        [SerializeField] private List<Entry> _slots = new();
        [SerializeField] private List<Entry> _modules = new();

        public Sprite Skill(string id) => Find(_skills, id);
        public Sprite Slot(string slotId) => Find(_slots, slotId);
        public Sprite Module(string moduleId) => Find(_modules, moduleId);

        /// <summary>The module's icon, else its slot's; null when neither has one.</summary>
        public Sprite ModuleOrSlot(string moduleId, string slotId)
        {
            Sprite sprite = Find(_modules, moduleId);
            return sprite != null ? sprite : Find(_slots, slotId);
        }

        /// <summary>
        /// Shows the icon in the symbol image, tinted; without a sprite (or without a symbol image) the glyph
        /// shows the short text in the tint instead.
        /// </summary>
        public static void Show(Image symbol, TMP_Text glyph, Sprite sprite, string glyphText, Color tint)
        {
            bool hasIcon = sprite != null && symbol != null;
            if (symbol != null)
            {
                symbol.enabled = hasIcon;
                if (hasIcon)
                {
                    symbol.sprite = sprite;
                    symbol.color = tint;
                }
            }

            if (glyph != null)
            {
                glyph.enabled = !hasIcon;
                if (!hasIcon)
                {
                    glyph.text = glyphText;
                    glyph.color = tint;
                }
            }
        }

        /// <summary>A state colour softened toward white, so a white icon keeps its contrast on the dark frames.</summary>
        public static Color Tint(Color color) => Color.Lerp(color, Color.white, 0.35f);

        private static Sprite Find(List<Entry> entries, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id)
                    return entries[i].sprite;
            }
            return null;
        }
    }
}
