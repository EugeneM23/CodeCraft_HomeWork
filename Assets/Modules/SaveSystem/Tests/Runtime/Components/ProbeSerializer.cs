using Newtonsoft.Json.Linq;
using Saving.Objects;

namespace Saving.Tests
{
    /// <summary>Version 2 of the probe's data: version 1 stored the speed as "spd".</summary>
    public sealed class ProbeSerializer : ComponentSerializer<SerializerProbe, ProbeSerializer.Data>
    {
        public sealed class Data
        {
            public float Speed;
            public string Mode;
        }

        public override int Version => 2;

        protected override Data Serialize(SerializerProbe probe) => new() { Speed = probe.Speed, Mode = probe.Mode };

        protected override void Deserialize(SerializerProbe probe, Data data)
        {
            probe.Speed = data.Speed;
            probe.Mode = data.Mode;
        }

        protected override JToken Upgrade(JToken data, int fromVersion)
        {
            if (fromVersion != 1)
                return base.Upgrade(data, fromVersion);

            var json = (JObject)data;
            json["speed"] = json["spd"];
            json.Remove("spd");
            return json;
        }
    }
}
