using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct ApplyDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (damageRequest, health, entity) in SystemAPI.Query<DamageRequest, RefRW<Health>>()
                     .WithEntityAccess())
        {
            health.ValueRW.Value -= damageRequest.DamageAmount;

            var hitEffect = state.EntityManager.GetComponentData<HitEffect>(entity);
            var transform = state.EntityManager.GetComponentData<LocalTransform>(entity);

            ecb.AddComponent(entity, new SpawnPrefabRequest
            {
                Prefab = hitEffect.Prefab,
                Position = transform.Position
            });

            ecb.AddComponent(entity, new DamageSoundRequest
            {
                Target = entity  // Передаём сущность, которая получила урон
            });
            
            ecb.RemoveComponent<DamageRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}