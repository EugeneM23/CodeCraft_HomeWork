using AudioEngine;
using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Game.Scripts.UI.Entities.Components.Health;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ProjectileMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var manager = state.EntityManager;

        foreach (var (transform, target, speed, entity) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRO<Target>, RefRO<Speed>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            if (target.ValueRO.Value == Entity.Null || !manager.Exists(target.ValueRO.Value))
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            var targetTransform = manager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            float distanceSq = math.distancesq(transform.ValueRO.Position, targetTransform.Position);

            if (distanceSq < 2f)
            {
                ecb.DestroyEntity(entity);

                if (manager.HasComponent<Health>(target.ValueRO.Value) && manager.HasComponent<Damage>(entity))
                {
                    var damage = manager.GetComponentData<Damage>(entity);
                    RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(target.ValueRO.Value);
                    targetHealth.ValueRW.Value -= damage.Value;

                    if (targetHealth.ValueRO.Value <= 0)
                    {
                        Entity deadEntity = target.ValueRO.Value;

                        if (manager.HasComponent<PlayerTag>(deadEntity))
                            ecb.RemoveComponent<PlayerTag>(deadEntity);

                        if (manager.HasComponent<EnemyTag>(deadEntity))
                            ecb.RemoveComponent<EnemyTag>(deadEntity);

                        ecb.RemoveComponent<Target>(deadEntity);
                        ecb.AddComponent(deadEntity, new AnimationEventRequest { Parameter = "Death" });

                        if (manager.HasComponent<AgentBody>(deadEntity))
                        {
                            ecb.RemoveComponent<AgentBody>(deadEntity);
                        }

                        AudioSystem.Instance.PlayEvent(MasterBankAPI.DeathEvent, targetTransform.Position);
                    }

                    if (manager.HasComponent<HitEffect>(target.ValueRO.Value))
                    {
                        Entity hitEffectRequest = manager.CreateEntity();
                        ecb.AddComponent(hitEffectRequest, new SpawnPrefabRequest
                        {
                            Position = targetTransform.Position,
                            Prefab = manager.GetComponentData<HitEffect>(target.ValueRO.Value).Prefab
                        });
                    }

                    AudioSystem.Instance.PlayEvent(MasterBankAPI.ElectrickHitEvent, targetTransform.Position);
                }

                if (manager.HasComponent<ProjectileHitPrefab>(entity))
                {
                    var hitPrefab = manager.GetComponentData<ProjectileHitPrefab>(entity);
                    var hitEntity = ecb.Instantiate(hitPrefab.Value);
                    ecb.SetComponent(hitEntity, LocalTransform.FromPosition(targetTransform.Position));
                }

                continue;
            }

            RotateUseCase.RotateTowardsPosition(ref transform.ValueRW, targetTransform.Position, 360f, deltaTime);
            MoveUseCase.Move(ref transform.ValueRW, targetTransform.Position, speed.ValueRO.Value, deltaTime);
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}