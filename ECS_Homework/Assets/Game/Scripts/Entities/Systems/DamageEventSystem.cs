using System;
using AudioEngine;
using Game.Scripts.Entities.Systems;
using Rukhanka;
using Rukhanka.Toolbox;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Game.Scripts.UI.Entities.Components.Health
{
    [UpdateAfter(typeof(RukhankaAnimationSystemGroup))]
    public partial struct DamageEventSystem : ISystem
    {
        private uint _damageHash;

        public void OnCreate(ref SystemState state)
        {
            _damageHash = FixedStringExtensions.CalculateHash32(new FixedString64Bytes("DealDamage"));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var manager = state.EntityManager;

            foreach (var (animEvents, entity) in SystemAPI.Query<DynamicBuffer<AnimationEventComponent>>()
                         .WithEntityAccess())
            {
                foreach (var evt in animEvents)
                {
                    if (evt.nameHash == _damageHash)
                    {
                        // ДОБАВЬТЕ ЭТУ ПРОВЕРКУ
                        if (!manager.HasComponent<Target>(entity))
                            continue;

                        Target target = manager.GetComponentData<Target>(entity);

                        if (target.Value == Entity.Null ||
                            !manager.Exists(target.Value) ||
                            !manager.HasComponent<Health>(target.Value))
                            continue;

                        // ТАКЖЕ ПРОВЕРЬТЕ НАЛИЧИЕ КОМПОНЕНТА Damage
                        if (!manager.HasComponent<Damage>(entity))
                            continue;

                        Damage damage = manager.GetComponentData<Damage>(entity);
                        RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(target.Value);

                        targetHealth.ValueRW.Value -= damage.Value;
                        var transform = manager.GetComponentData<LocalTransform>(target.Value);

                        if (targetHealth.ValueRO.Value <= 0)
                        {
                            Entity deadEntity = target.Value;

                            if (manager.HasComponent<PlayerTag>(deadEntity))
                                ecb.RemoveComponent<PlayerTag>(deadEntity);

                            if (manager.HasComponent<EnemyTag>(deadEntity))
                                ecb.RemoveComponent<EnemyTag>(deadEntity);

                            ecb.RemoveComponent<Target>(deadEntity);
                            ecb.SetComponent(entity, new Target { Value = Entity.Null });
                            ecb.AddComponent(deadEntity, new AnimationEventRequest { Parameter = "Death" });

                            AudioSystem.Instance.PlayEvent(MasterBankAPI.DeathEvent, transform.Position);
                        }

                        //Effect
                        Entity hitEffectRequset = manager.CreateEntity();

                        ecb.AddComponent(hitEffectRequset, new SpawnPrefabRequest
                        {
                            Position = transform.Position,
                            Prefab = manager.GetComponentData<HitEffect>(target.Value).Prefab
                        });

                        AudioSystem.Instance.PlayEvent(MasterBankAPI.FootmanHitEvent, transform.Position);
                    }
                }
            }

            ecb.Playback(manager);
            ecb.Dispose();
        }
    }
}