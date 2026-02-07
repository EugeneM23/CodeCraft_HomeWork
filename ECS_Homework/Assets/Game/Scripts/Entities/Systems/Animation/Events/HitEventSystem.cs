using Game.Animation;
using Game.Scripts.Entities.Systems;
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

                    ecb.AddComponent(target.Value, new DamageRequest
                    {
                        Target = target.Value,
                        DamageAmount = damage.Value
                    });

                    ecb.AddComponent(target.Value, new SpawnPrefabRequest
                    {
                        Prefab = hitEffect.Prefab,
                        Position = targetTransform.Position,
                        Rotaion = targetTransform.Rotation
                    });

                    ecb.AddComponent(target.Value, new DamageSoundRequest());
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}