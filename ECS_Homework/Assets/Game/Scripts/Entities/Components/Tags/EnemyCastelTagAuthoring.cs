using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class EnemyCastelTagAuthoring : MonoBehaviour
    {
        private class EnemyCastelTagBaker : Baker<EnemyCastelTagAuthoring>
        {
            public override void Bake(EnemyCastelTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemyCastelTag());
            }
        }
    }

    public struct EnemyCastelTag : IComponentData
    {
    }
}