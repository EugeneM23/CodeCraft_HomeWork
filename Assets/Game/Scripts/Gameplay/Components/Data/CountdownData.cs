using System;
using Newtonsoft.Json;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    [Serializable]
    public struct CountdownData
    {
        [SaveField] [JsonProperty] private readonly float Current;
        [SaveField] [JsonProperty] private readonly float Duration;

        public CountdownData(float current, float duration)
        {
            Current = current;
            Duration = duration;
        }
    }
}