using AudioEngine;
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

                    var damageRequest = ecb.CreateEntity();
                    ecb.AddComponent(damageRequest, new DamageRequest
                    {
                        Target = target.Value,
                        DamageAmount = damage.Value
                    });

                    var spawnRequest = ecb.CreateEntity();
                    ecb.AddComponent(spawnRequest, new SpawnPrefabRequest
                    {
                        Prefab = hitEffect.Prefab,
                        Position = targetTransform.Position,
                        Rotaion = targetTransform.Rotation
                    });

                    var soundRequest = ecb.CreateEntity();
                    ecb.AddComponent(soundRequest, new DamageSoundRequest()
                    {
                        Target = target.Value,
                        SoundName = MasterBankAPI.FootmanHitEvent
                    });
                }
            }

            eventBuffer.Clear();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}