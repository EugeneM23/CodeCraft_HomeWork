using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class EnemyTagAuthoring : MonoBehaviour
    {
        class Baker : Baker<EnemyTagAuthoring>
        {
            public override void Bake(EnemyTagAuthoring tagAuthoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
            }
        }
    }

    public struct EnemyTag : IComponentData
    {
    }
}