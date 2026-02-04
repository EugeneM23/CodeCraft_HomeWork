using Game.Animation;
using Game.Scripts.Entities.Systems;
using Rukhanka;
using Rukhanka.Toolbox;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(RukhankaAnimationSystemGroup))]
public partial struct ProjectileSpawntSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = state.EntityManager;

        foreach (var (animEvents, entity) in SystemAPI.Query<DynamicBuffer<AnimationEventComponent>>()
                     .WithEntityAccess())
        {
            foreach (var evt in animEvents)
            {
                if (evt.nameHash == AnimationEventType.SpawnProjectile.ToEventName().CalculateHash32())
                {
                    var prefab = state.EntityManager.GetComponentData<ProjectilePrefab>(entity);
                    Entity projectile = state.EntityManager.Instantiate(prefab.Value);

                    var entityTarget = state.EntityManager.GetComponentData<Target>(entity);
                    state.EntityManager.SetComponentData(projectile, new Target { Value = entityTarget.Value });

                    var transform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                    var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(entityTarget.Value);
                    
                    float3 spawnPosition = transform.Position + new float3(0, 1f, 0);
                    float3 direction = targetTransform.Position - spawnPosition;
                    direction.y = 0f;
                    
                    quaternion rotation = quaternion.identity;
                    if (math.lengthsq(direction) > 0.001f)
                    {
                        direction = math.normalize(direction);
                        rotation = quaternion.LookRotationSafe(direction, math.up());
                    }

                    state.EntityManager.SetComponentData(projectile, new LocalTransform
                    {
                        Position = spawnPosition,
                        Rotation = rotation,
                        Scale = 1f,
                    });
                }
            }
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}