using Game.Animation;
using Game.Scripts.Entities.Systems;
using Game.Scripts.Entities.Systems.Request;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
                    ecb.AddComponent(projectilePrefab.Value, new Target { Value = target.Value });
                    RequestUseCase.SpawnPrefab(ecb, projectilePrefab.Value, transform.Position + new float3(0, 1, 0),
                        transform.Rotation);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}