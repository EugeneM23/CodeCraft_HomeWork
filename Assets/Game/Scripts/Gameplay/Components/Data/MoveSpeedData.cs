using Newtonsoft.Json;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal struct MoveSpeedData
    {
        [SaveField] [JsonProperty] public readonly float Current;

        public MoveSpeedData(float current)
        {
            Current = current;
        }
    }
}