using Newtonsoft.Json.Linq;
using Saving;

namespace Game
{
    /// <summary>
    /// The "progress" section of the progress save: <see cref="SaveData"/>. Bump VERSION and add an Upgrade step when
    /// the shape changes. Version 1 was the old PlayerProgress (<see cref="ProgressDataV1"/>), version 2 is GDD §14.
    /// </summary>
    public sealed class ProgressSerializer : GameSerializer<MetaProgress, SaveData>
    {
        public const string KEY = "progress";
        public const int VERSION = 2;

        public ProgressSerializer(MetaProgress progress) : base(progress, KEY, VERSION)
        {
        }

        protected override SaveData Serialize(MetaProgress progress) => progress.Data;

        protected override void Deserialize(MetaProgress progress, SaveData data) => progress.Restore(data);

        protected override JToken Upgrade(JToken data, int fromVersion) => fromVersion switch
        {
            1 => ProgressMigration.FromV1(data, Service.Catalog),
            _ => base.Upgrade(data, fromVersion)
        };
    }
}
