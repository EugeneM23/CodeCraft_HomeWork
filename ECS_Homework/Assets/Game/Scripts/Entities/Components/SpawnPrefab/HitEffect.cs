using Unity.Entities;

namespace Game.Scripts.UI.Entities.Components.Health
{
    public struct HitEffect : IComponentData
    {
        public Entity Prefab;
    }
}