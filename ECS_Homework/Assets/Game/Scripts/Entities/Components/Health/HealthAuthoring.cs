using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.UI.Entities.Components.Health
{
    public class HealthAuthoring : MonoBehaviour
    {
        public int Health = 100;

        private class HealthBaker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Health { Value = authoring.Health });
            }
        }
    }
    
    public struct Health : IComponentData
    {
        public int Value;
    }
}