using Newtonsoft.Json.Linq;
using Saving;

namespace Game
{
    /// <summary>The "settings" section of the settings save. Bump VERSION and add an Upgrade step when the shape changes.</summary>
    public sealed class SettingsSerializer : GameSerializer<SettingsStore, GameSettings>
    {
        public const string KEY = "settings";
        public const int VERSION = 1;

        public SettingsSerializer(SettingsStore store) : base(store, KEY, VERSION)
        {
        }

        protected override GameSettings Serialize(SettingsStore store) => store.Current;

        protected override void Deserialize(SettingsStore store, GameSettings data) => store.Restore(data);

        protected override JToken Upgrade(JToken data, int fromVersion) => fromVersion switch
        {
            // 1 => ...,  // one case per format change: version 1 -> 2, and so on
            _ => base.Upgrade(data, fromVersion)
        };
    }
}
