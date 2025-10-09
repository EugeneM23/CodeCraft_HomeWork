using Newtonsoft.Json;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal struct HealthData
    {
        [SaveField] [JsonProperty] public readonly int Current;
        [SaveField] [JsonProperty] public readonly int Max;

        public HealthData(int current, int max)
        {
            Current = current;
            Max = max;
        }
    }
}