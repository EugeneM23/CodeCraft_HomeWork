using UnityEngine;

namespace Game
{
    /// <summary>
    /// How a run skill looks until it has an icon: a coloured square with a two-letter glyph. Texts by id from
    /// the string table: skill.&lt;id&gt;.name, .glyph, .desc (level 1), .desc_up (next levels). The keys are
    /// built once per id and kept, so views can ask for them on change without new strings.
    /// </summary>
    public static class SkillVisuals
    {
        // The core's SkillIds of milestone 1
        private static readonly string[] Ids =
        {
            SkillIds.Turret.Value, SkillIds.AirStrike.Value, SkillIds.Ricochet.Value,
            SkillIds.RateOfFire.Value, SkillIds.Armor.Value, SkillIds.Magnet.Value
        };

        private static readonly Color[] Colors =
        {
            new(0.85f, 0.55f, 0.2f, 1f),  // turret
            new(0.85f, 0.3f, 0.25f, 1f),  // air_strike
            new(0.35f, 0.65f, 0.9f, 1f),  // ricochet
            new(0.95f, 0.8f, 0.25f, 1f),  // rate_of_fire
            new(0.45f, 0.55f, 0.65f, 1f), // armor
            new(0.6f, 0.4f, 0.85f, 1f)    // magnet
        };

        private static readonly Color Unknown = new(0.4f, 0.42f, 0.46f, 1f);

        private static readonly System.Collections.Generic.Dictionary<string, string[]> Keys = new();

        public static Color ColorOf(string id)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                if (Ids[i] == id)
                    return Colors[i];
            }
            return Unknown;
        }

        public static string NameKey(string id) => KeysOf(id)[0];
        public static string GlyphKey(string id) => KeysOf(id)[1];
        public static string DescKey(string id) => KeysOf(id)[2];
        public static string DescUpKey(string id) => KeysOf(id)[3];

        /// <summary>The description of the level the skill goes to: level 1 text for a new skill, the growth text after.</summary>
        public static string DescriptionKey(string id, int toLevel) => toLevel <= 1 ? DescKey(id) : DescUpKey(id);

        private static string[] KeysOf(string id)
        {
            id ??= string.Empty;
            if (Keys.TryGetValue(id, out string[] keys))
                return keys;

            string prefix = "skill." + id;
            keys = new[] { prefix + ".name", prefix + ".glyph", prefix + ".desc", prefix + ".desc_up" };
            Keys.Add(id, keys);
            return keys;
        }
    }
}
