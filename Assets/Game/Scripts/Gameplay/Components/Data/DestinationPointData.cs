using Newtonsoft.Json;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    internal struct DestinationPointData
    {
        [SaveField] [JsonProperty] public readonly Vector3 Value;

        public DestinationPointData(Vector3 value)
        {
            Value = value;
        }
    }
}