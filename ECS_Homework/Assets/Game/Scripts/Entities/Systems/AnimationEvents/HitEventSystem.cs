using AudioEngine;
using Game.Animation;
using Game.Scripts.Entities.Systems;
using Game.Scripts.Entities.Systems.Request;
using Game.Scripts.UI.Entities.Components;
using Game.Scripts.UI.Entities.Components.Health;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(SpawnPrefabSystem))]
public partial struct HitEventSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorRef, target, damage, hitEffect) in SystemAPI
                     .Query<AnimatorEntityRefComponent, Target, Damage, HitEffect>())
        {
            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorRef.animatorEntity);

            foreach (var animEvent in eventBuffer)
            {
                if (animEvent.nameHash == (uint)AnimationEventType.DealDamage)
                {
                    var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);

                    RequestUseCase.DealDamage(ecb, target.Value, damage.Value);
                    RequestUseCase.SpawnPrefab(ecb, hitEffect.Prefab, targetTransform.Position, targetTransform.Rotation);
                    RequestUseCase.PlaySound(ecb, target.Value, MasterBankAPI.FootmanHitEvent, targetTransform.Position);
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}