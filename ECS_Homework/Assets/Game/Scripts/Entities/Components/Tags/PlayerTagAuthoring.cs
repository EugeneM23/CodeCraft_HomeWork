using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class PlayerTagAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerTagAuthoring>
        {
            public override void Bake(PlayerTagAuthoring tagAuthoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
            }
        }
    }

    public struct PlayerTag : IComponentData
    {
    }
}