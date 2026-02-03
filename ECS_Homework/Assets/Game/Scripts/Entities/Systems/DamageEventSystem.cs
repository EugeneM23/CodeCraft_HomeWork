using System;
using Game.Scripts.Entities.Systems;
using Rukhanka;
using Rukhanka.Toolbox;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

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
                        Target target = manager.GetComponentData<Target>(entity);

                        // Проверки target
                        if (target.Value == Entity.Null ||
                            !manager.Exists(target.Value) ||
                            !manager.HasComponent<Health>(target.Value))
                            continue;

                        Damage damage = manager.GetComponentData<Damage>(entity);
                        RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(target.Value);

                        targetHealth.ValueRW.Value -= damage.Value;

                        if (targetHealth.ValueRO.Value <= 0)
                        {
                            Entity deadEntity = target.Value;
                            ecb.SetComponent(entity, new Target { Value = Entity.Null });
                            ecb.DestroyEntity(deadEntity);
                        }

                        Entity hitEffectRequset = manager.CreateEntity();
                        ecb.AddComponent(hitEffectRequset, new SpawnPrefabRequest()
                        {
                            Position = manager.GetComponentData<LocalTransform>(target.Value).Position,
                            Prefab = manager.GetComponentData<HitEffect>(target.Value).Prefab
                        });
                    }
                }
            }

            ecb.Playback(manager);
            ecb.Dispose();
        }
    }
}