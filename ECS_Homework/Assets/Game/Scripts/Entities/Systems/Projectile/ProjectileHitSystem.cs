using AudioEngine;
using Game.Scripts.Entities.Systems;
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
                     .Query<Target, RefRO<Damage>, DistanceToTarget>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
            var radius = state.EntityManager.GetComponentData<EntityRadius>(target.Value);


            if (distanceToTarget.Value < radius.Value / 2)
            {
                var damageReqst = ecb.CreateEntity();
                ecb.AddComponent(damageReqst, new DamageRequest
                {
                    Target = target.Value,
                    DamageAmount = damage.ValueRO.Value
                });

                var hitPrefab = state.EntityManager.GetComponentData<ProjectileHitPrefab>(entity);
                var projectileHit = ecb.CreateEntity();
                ecb.AddComponent(projectileHit, new SpawnPrefabRequest
                {
                    Prefab = hitPrefab.Value,
                    Position = targetTransform.Position + new float3(0, 1f, 0),
                    Rotaion = targetTransform.Rotation
                });

                var hitEffect = state.EntityManager.GetComponentData<HitEffect>(target.Value);
                var targeHit = ecb.CreateEntity();
                ecb.AddComponent(targeHit, new SpawnPrefabRequest
                {
                    Prefab = hitEffect.Prefab,
                    Position = targetTransform.Position,
                    Rotaion = targetTransform.Rotation
                });

                var soundRequest = ecb.CreateEntity();
                ecb.AddComponent(soundRequest, new AudioRequest
                {
                    Target = target.Value,
                    SoundName = MasterBankAPI.ElectrickHitEvent,
                    Position = targetTransform.Position
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