using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class PlayerCastleTagAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerCastleTagAuthoring>
        {
            public override void Bake(PlayerCastleTagAuthoring tagAuthoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerCastleTag>(entity);
            }
        }
    }
}