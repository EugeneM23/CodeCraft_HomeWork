using Game.Animation;
using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Unity.Entities;
using Rukhanka;
using Unity.Collections;
using UnityEngine;

public partial struct AnimationEventListenerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorRef, target, entity) in SystemAPI.Query<AnimatorEntityRefComponent, RefRO<Target>>()
                     .WithEntityAccess())
        {
            var animatorEntity = animatorRef.animatorEntity;

            if (!state.EntityManager.HasBuffer<AnimationEventComponent>(animatorEntity))
                continue;

            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorEntity);


            foreach (AnimationEventComponent animEvent in eventBuffer)
            {
                if (animEvent.nameHash == (uint)AnimationEventType.DealDamage)
                {
                    // Проверка, существует ли entity
                    if (!state.EntityManager.Exists(entity))
                        continue;

                    if (!state.EntityManager.HasComponent<Damage>(entity))
                        continue;

                    var damage = state.EntityManager.GetComponentData<Damage>(entity);

                    var targetEntity = target.ValueRO.Value;

                    // Проверка, существует ли target
                    if (targetEntity == Entity.Null)
                        continue;

                    if (!state.EntityManager.Exists(targetEntity))
                        continue;

                    ecb.AddComponent(targetEntity, new DamageRequest
                    {
                        Target = targetEntity,
                        DamageAmount = damage.Value
                    });
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}