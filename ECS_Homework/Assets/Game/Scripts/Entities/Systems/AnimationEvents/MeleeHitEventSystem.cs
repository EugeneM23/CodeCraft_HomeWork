using AudioEngine;
using Game.Animation;
using Game.Scripts.Entities.Systems;
using Game.Scripts.Entities.Systems.Request;
using Game.Scripts.UI.Entities.Components;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(SpawnPrefabSystem))]
public partial struct MeleeHitEventSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorRef, target, damage) in SystemAPI
                     .Query<AnimatorEntityRefComponent, Target, Damage>()
                     .WithNone<IsDead>())
        {
            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorRef.animatorEntity);

            foreach (var animEvent in eventBuffer)
            {
                if (animEvent.nameHash == AnimationEventType.DealDamage)
                {
                    var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);

                    RequestUseCase.DealDamage(ecb, target.Value, damage.Value);
                    RequestUseCase.PlaySound(ecb, target.Value, MasterBankAPI.FootmanHitEvent, targetTransform.Position);
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}