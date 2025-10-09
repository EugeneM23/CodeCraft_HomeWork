using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal class HealthSerializer : GameSerializer<Health, HealthData>
    {
        protected override HealthData Serialize(Health health) => new(health.Current, health.Max);
    }
}