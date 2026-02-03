using Unity.Entities;

namespace Game.Scripts.Entities.Systems
{
    public struct Target : IComponentData
    {
        public Entity Value;
        public float UpdateTimer;
        public float UpdateInterval;
    }
}