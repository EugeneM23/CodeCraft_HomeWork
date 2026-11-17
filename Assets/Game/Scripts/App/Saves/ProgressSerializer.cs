using Newtonsoft.Json.Linq;
using Saving;

namespace Game
{
    /// <summary>The "progress" section of the progress save. Bump VERSION and add an Upgrade step when the shape changes.</summary>
    public sealed class ProgressSerializer : GameSerializer<PlayerProgress, ProgressData>
    {
        public const string KEY = "progress";
        public const int VERSION = 1;

        public ProgressSerializer(PlayerProgress progress) : base(progress, KEY, VERSION)
        {
        }

        protected override ProgressData Serialize(PlayerProgress progress) => progress.Data;

        protected override void Deserialize(PlayerProgress progress, ProgressData data) => progress.Restore(data);

        protected override JToken Upgrade(JToken data, int fromVersion) => fromVersion switch
        {
            // 1 => ...,  // one case per format change: version 1 -> 2, and so on
            _ => base.Upgrade(data, fromVersion)
        };
    }
}
