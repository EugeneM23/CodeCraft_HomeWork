using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(SpawnPrefabSystem))]
public partial struct ProjectileHitSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (target, damage, distanceToTarget, entity) in SystemAPI
                     .Query<Target, RefRO<Damage>, DistanceToTarget>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
            var radius = state.EntityManager.GetComponentData<EntityRadius>(target.Value);
            

            if (distanceToTarget.Value < radius.Value / 2)
            {
                ecb.AddComponent(target.Value, new DamageRequest()
                {
                    Target = target.Value,
                    DamageAmount = damage.ValueRO.Value
                });

                var hitPrefab = state.EntityManager.GetComponentData<ProjectileHitPrefab>(entity);

                ecb.AddComponent(target.Value, new SpawnPrefabRequest
                {
                    Prefab = hitPrefab.Value,
                    Position = targetTransform.Position + new float3(0, 1f, 0),
                    Rotaion = targetTransform.Rotation
                });

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

internal struct EntityRadius : IComponentData
{
    public float Value;
}