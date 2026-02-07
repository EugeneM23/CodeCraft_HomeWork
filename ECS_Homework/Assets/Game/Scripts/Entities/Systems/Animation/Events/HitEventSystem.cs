using Game.Animation;
using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;

public partial struct HitEventSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorRef, target, damage, entity) in SystemAPI
                     .Query<AnimatorEntityRefComponent, RefRO<Target>, RefRO<Damage>>()
                     .WithEntityAccess())
        {
            if (!state.EntityManager.HasBuffer<AnimationEventComponent>(animatorRef.animatorEntity))
                continue;

            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorRef.animatorEntity);

            foreach (var animEvent in eventBuffer)
            {
                if (animEvent.nameHash == (uint)AnimationEventType.DealDamage && target.ValueRO.Value != Entity.Null)
                {
                    ecb.AddComponent(target.ValueRO.Value, new DamageRequest
                    {
                        Target = target.ValueRO.Value,
                        DamageAmount = damage.ValueRO.Value
                    });

                    ecb.AddComponent(target.ValueRO.Value, new HitEffectRequest());
                    ecb.AddComponent(target.ValueRO.Value, new DamageSoundRequest());
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}