using Newtonsoft.Json;

namespace SampleGame.Gameplay
{
    internal struct TargetObjectData
    {
        [JsonProperty] public readonly int Id;

        public TargetObjectData(int id)
        {
            this.Id = id;
        }
    }
}