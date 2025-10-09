using System.Collections.Generic;
using Newtonsoft.Json;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal struct ProductionOrderData
    {
        [SaveField] [JsonProperty] public readonly List<string> ConfigNames;

        public ProductionOrderData(List<string> configNames)
        {
            ConfigNames = configNames;
        }
    }
}