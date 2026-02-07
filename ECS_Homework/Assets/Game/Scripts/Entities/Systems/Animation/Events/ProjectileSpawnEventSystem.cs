using Game.Animation;
using Game.Scripts.Entities.Systems;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(SpawnPrefabSystem))]
public partial struct ProjectileSpawnEventSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorRef, target, projectilePrefab, transform) in SystemAPI
                     .Query<AnimatorEntityRefComponent, Target, ProjectilePrefab, LocalTransform>())
        {
            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorRef.animatorEntity);

            foreach (var animEvent in eventBuffer)
            {
                if (animEvent.nameHash == (uint)AnimationEventType.SpawnProjectile)
                {
                    var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
                    SpawnUseCase.Projectile(target, transform, targetTransform, ecb, projectilePrefab);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}