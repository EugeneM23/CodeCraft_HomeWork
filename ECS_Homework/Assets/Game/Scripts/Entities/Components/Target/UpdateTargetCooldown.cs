using Unity.Entities;

namespace Game.Scripts.Entities.Systems
{
    public struct TargetUpdateSettings : IComponentData
    {
        public float Range;
        public float UpdateTimer;
        public float UpdateInterval;
    }
}