using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How the menus show the source's ids: names and descriptions from UiStrings by key (module.&lt;id&gt;.name,
    /// module.&lt;id&gt;.desc, chapter.&lt;id&gt;.name, tank.&lt;id&gt;.name, slot.&lt;id&gt;) and the slot icon: a
    /// coloured square with a short glyph (slot.&lt;id&gt;.glyph). Texts are cached per id and language, so a
    /// redraw builds no key strings.
    /// </summary>
    public static class HangarLook
    {
        // Slot colours by ModuleSlot index, from the one UiStyle palette
        private static readonly Color[] SlotColors =
        {
            UiStyle.Accent,
            UiStyle.Bad,
            UiStyle.Armor,
            UiStyle.Good,
            UiStyle.TextMuted,
            Color.Lerp(UiStyle.Accent, UiStyle.Armor, 0.5f)
        };

        // UiStrings keys by (int)PurchaseResult
        private static readonly string[] ResultKeys =
        {
            "hangar.purchase.done", "hangar.purchase.not_enough_marks", "hangar.purchase.not_enough_samples",
            "hangar.purchase.already_owned", "hangar.purchase.not_owned", "hangar.purchase.max_level",
            "hangar.purchase.locked", "hangar.purchase.unknown"
        };

        // UiStrings keys of the slot names and glyphs, by ModuleSlot index (keys are lowercase)
        private static readonly string[] SlotNameKeys =
            { "slot.gun", "slot.machine_gun", "slot.armor", "slot.engine", "slot.tracks", "slot.special" };
        private static readonly string[] SlotGlyphKeys =
        {
            "slot.gun.glyph", "slot.machine_gun.glyph", "slot.armor.glyph", "slot.engine.glyph", "slot.tracks.glyph",
            "slot.special.glyph"
        };

        private static readonly Cache ModuleNames = new("module.", ".name");
        private static readonly Cache ModuleDescriptions = new("module.", ".desc");
        private static readonly Cache ChapterNames = new("chapter.", ".name");
        private static readonly Cache TankNames = new("tank.", ".name");

        public static string SlotName(string slotId) => BySlot(SlotNameKeys, slotId);
        public static string SlotGlyph(string slotId) => BySlot(SlotGlyphKeys, slotId);
        public static string ModuleName(string moduleId) => ModuleNames.Get(moduleId);
        public static string ModuleDescription(string moduleId) => ModuleDescriptions.Get(moduleId);
        public static string ChapterName(string chapterId) => ChapterNames.Get(chapterId);
        public static string TankName(string tankId) => TankNames.Get(tankId);

        /// <summary>Why a purchase or an upgrade did not go through (or "done"), for a button or an error line.</summary>
        public static string Result(PurchaseResult result)
        {
            int index = (int)result;
            return index >= 0 && index < ResultKeys.Length ? UiStrings.Get(ResultKeys[index]) : string.Empty;
        }

        /// <summary>"300 марок" or "600 марок + 5 образцов" (TMP SetText, no string built).</summary>
        public static void SetPrice(TMP_Text text, int marks, int samples)
        {
            if (samples > 0)
                text.SetText(UiStrings.Get("hangar.price_both_format"), marks, samples);
            else
                text.SetText(UiStrings.Get("hangar.price_marks_format"), marks);
        }

        private static string BySlot(string[] keys, string slotId)
        {
            int index = HangarSlots.IndexOf(slotId);
            return index >= 0 && index < keys.Length ? UiStrings.Get(keys[index]) : string.Empty;
        }

        public static Color SlotColor(string slotId)
        {
            int index = HangarSlots.IndexOf(slotId);
            return index >= 0 && index < SlotColors.Length ? SlotColors[index] : UiStyle.Button;
        }

        private sealed class Cache
        {
            private readonly string _prefix;
            private readonly string _suffix;
            private readonly Dictionary<string, string> _texts = new();
            private UiLanguage _language;

            public Cache(string prefix, string suffix)
            {
                _prefix = prefix;
                _suffix = suffix;
            }

            public string Get(string id)
            {
                if (string.IsNullOrEmpty(id))
                    return string.Empty;

                if (_language != UiStrings.Language)
                {
                    _language = UiStrings.Language;
                    _texts.Clear();
                }

                if (!_texts.TryGetValue(id, out string text))
                {
                    text = UiStrings.Get(_prefix + id + _suffix);
                    _texts.Add(id, text);
                }
                return text;
            }
        }
    }
}
