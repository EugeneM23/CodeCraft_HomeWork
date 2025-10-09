using Newtonsoft.Json;
using SampleGame.Common;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal struct ResourceBagData
    {
        [SaveField] [JsonProperty] public readonly ResourceType Type;
        [SaveField] [JsonProperty] public readonly int Current;
        [SaveField] [JsonProperty] public readonly int Capacity;

        public ResourceBagData(ResourceType type, int current, int capacity)
        {
            Type = type;
            Current = current;
            Capacity = capacity;
        }
    }
}