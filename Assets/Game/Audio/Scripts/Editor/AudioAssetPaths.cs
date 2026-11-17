using System.Text;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Where the audio assets of the game live: banks, events, the clip folders the user drops files into,
    /// the UI and music catalogs, the mixer. One place so the sync, the import rules and the sound board agree.
    /// </summary>
    internal static class AudioAssetPaths
    {
        public const string ROOT = "Assets/Game/Audio";
        public const string BANKS = ROOT + "/Banks";
        public const string EVENTS = ROOT + "/Events";
        public const string CLIPS = ROOT + "/Clips";
        public const string MUSIC = ROOT + "/Music";
        public const string UI = ROOT + "/UI";
        public const string MIXER = ROOT + "/AudioMixer.mixer";

        public const string UI_CATALOG = UI + "/UISounds.asset";
        public const string MENU_MUSIC = MUSIC + "/MenuMusic.asset";
        public const string BATTLE_MUSIC = MUSIC + "/BattleMusic.asset";
        public const string FALLBACK_MUSIC = MUSIC + "/Music.wav";
        public const string RESOURCES = ROOT + "/Resources";
        public const string GAME_AUDIO_PREFAB = RESOURCES + "/GameAudio.prefab";
        public const string APP_AUDIO_PREFAB = ROOT + "/Prefabs/AppAudio.prefab";

        public const string UI_CLIPS = CLIPS + "/ui";
        public const string MENU_MUSIC_CLIPS = CLIPS + "/music/menu";
        public const string BATTLE_MUSIC_CLIPS = CLIPS + "/music/battle";

        public const string README_NAME = "README.txt";

        public static string KeyFolder(string bank, string key) => $"{CLIPS}/{bank}/{key}";

        public static string EventFolder(string bank) => $"{EVENTS}/{bank}";

        public static string EventAsset(string bank, string key) => $"{EVENTS}/{bank}/{bank}_{key}.asset";

        public static string BankAsset(string bank) => $"{BANKS}/{ToPascalCase(bank)}.asset";

        /// <summary>"wheel_spin" -> "WheelSpin".</summary>
        public static string ToPascalCase(string id)
        {
            StringBuilder builder = new StringBuilder(id.Length);
            bool upper = true;
            foreach (char c in id)
            {
                if (c == '_' || c == '.' || c == '-')
                {
                    upper = true;
                    continue;
                }

                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }

            return builder.ToString();
        }
    }
}
