using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Every interface string by key (UiStringTable) in the current language. Screens fetch a text once (Awake,
    /// Open, a click), never per frame; static labels follow LanguageChanged through UiLocalizedText.
    /// A missing key comes back as "[key]" with one warning per key.
    /// </summary>
    public static class UiStrings
    {
        private static readonly HashSet<string> Reported = new();
        private static UiLanguage s_language = UiLanguage.Russian;

        /// <summary>Raised after Language changed: static labels refresh themselves.</summary>
        public static event Action LanguageChanged;

        public static UiLanguage Language
        {
            get => s_language;
            set
            {
                if (s_language == value)
                    return;

                s_language = value;
                LanguageChanged?.Invoke();
            }
        }

        public static string Get(string key)
        {
            if (key != null && UiStringTable.For(s_language).TryGetValue(key, out string text))
                return text;

            if (Reported.Add(key ?? string.Empty))
                Debug.LogWarning($"[UiStrings] No text for key '{key}' in {s_language}");
            return "[" + key + "]";
        }

        /// <summary>Whether the current language's table has this key.</summary>
        public static bool Has(string key) => key != null && UiStringTable.For(s_language).ContainsKey(key);
    }
}
