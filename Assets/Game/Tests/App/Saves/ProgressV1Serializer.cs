using Saving;

namespace Game.Tests
{
    /// <summary>
    /// The old "progress" serializer (version 1, ProgressDataV1 under the same key) as PlayerProgress registered it:
    /// writes a real version 1 save for the migration tests.
    /// </summary>
    public sealed class ProgressV1Serializer : GameSerializer<ProgressDataV1, ProgressDataV1>
    {
        public ProgressV1Serializer(ProgressDataV1 data) : base(data, ProgressSerializer.KEY, version: 1)
        {
        }

        protected override ProgressDataV1 Serialize(ProgressDataV1 data) => data;

        protected override void Deserialize(ProgressDataV1 data, ProgressDataV1 read)
        {
        }
    }
}
