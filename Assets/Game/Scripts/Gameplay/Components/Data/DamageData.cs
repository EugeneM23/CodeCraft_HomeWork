using System;
using Newtonsoft.Json;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    [Serializable]
    public struct DamageData
    {
        [SaveField] [JsonProperty] private readonly int Value;

        public DamageData(int damage)
        {
            Value = damage;
        }
    }
}