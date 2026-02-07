using AudioEngine;
using Game.Scripts.Entities.Systems;
using Game.Scripts.Entities.Systems.Request;
using Game.Scripts.UI.Entities.Components;
using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(SpawnPrefabSystem))]
public partial struct ProjectileHitSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (target, damage, distanceToTarget, entity) in SystemAPI
                     .Query<Target, RefRO<Damage>, TargetDistance>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
            var radius = state.EntityManager.GetComponentData<EntityRadius>(target.Value);

            if (distanceToTarget.Value < radius.Value / 2)
            {
                var hitPrefab = state.EntityManager.GetComponentData<ProjectileHitPrefab>(entity);
                var hitEffect = state.EntityManager.GetComponentData<HitEffect>(target.Value);

                RequestUseCase.DealDamage(ecb, target.Value, damage.ValueRO.Value);
                RequestUseCase.SpawnPrefab(ecb, hitEffect.Prefab, targetTransform.Position, targetTransform.Rotation);
                RequestUseCase.SpawnPrefab(ecb, hitPrefab.Value, targetTransform.Position + new float3(0, 1f, 0), targetTransform.Rotation);
                RequestUseCase.PlaySound(ecb, target.Value, MasterBankAPI.ElectrickHitEvent, targetTransform.Position);

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}