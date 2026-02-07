using Game.Animation;
using Game.Scripts.Entities.Systems;
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

        foreach (var (animatorRef, target, projectilePrefab, transform, entity) in SystemAPI
                     .Query<AnimatorEntityRefComponent, Target, ProjectilePrefab, LocalTransform>().WithEntityAccess())
        {
            var eventBuffer = state.EntityManager.GetBuffer<AnimationEventComponent>(animatorRef.animatorEntity);
            
            foreach (var animEvent in eventBuffer)
            {
                if (animEvent.nameHash == (uint)AnimationEventType.SpawnProjectile)
                {
                    ecb.AddComponent(projectilePrefab.Value, new Target { Value = target.Value });
                    ecb.AddComponent(entity, new SpawnPrefabRequest
                    {
                        Prefab = projectilePrefab.Value,
                        Position = transform.Position + new float3(0, 1, 0),
                        Rotaion = transform.Rotation
                    });
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}