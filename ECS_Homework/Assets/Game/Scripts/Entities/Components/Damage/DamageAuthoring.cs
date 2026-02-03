using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.UI.Entities.Components
{
    public class DamageAuthoring : MonoBehaviour
    {
        public int DamageValue;

        private class DamageBaker : Baker<DamageAuthoring>
        {
            public override void Bake(DamageAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Damage { Value = authoring.DamageValue });
            }
        }
    }

    internal struct Damage : IComponentData
    {
        public int Value;
    }
}