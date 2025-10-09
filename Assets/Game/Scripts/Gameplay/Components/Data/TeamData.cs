using System;
using Newtonsoft.Json;
using SampleGame.Common;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    [Serializable]
    public class TeamData
    {
        [SaveField] [JsonProperty] private readonly TeamType Type;

        public TeamData(TeamType type) => Type = type;
    }
}