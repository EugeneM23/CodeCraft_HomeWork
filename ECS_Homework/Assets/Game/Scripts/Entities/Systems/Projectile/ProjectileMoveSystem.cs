using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ProjectileMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, target, speed, damage, entity) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRO<Target>, RefRO<Speed>, RefRO<Damage>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            float distanceSq = math.distancesq(transform.ValueRO.Position, targetTransform.Position);

            if (distanceSq < 2f)
            {
                ecb.AddComponent(target.ValueRO.Value, new DamageRequest()
                {
                    Target = target.ValueRO.Value,
                    DamageAmount = damage.ValueRO.Value
                });

                var hitPrefab = state.EntityManager.GetComponentData<ProjectileHitPrefab>(entity);
                var instantiate = ecb.Instantiate(hitPrefab.Value);

                ecb.SetComponent(instantiate,
                    LocalTransform.FromPosition(targetTransform.Position + new float3(0, 1f, 0)));
                
                ecb.AddComponent(target.ValueRO.Value, new HitEffectRequest());


                ecb.DestroyEntity(entity);
                continue;
            }

            RotateUseCase.RotateTowardsPosition(ref transform.ValueRW, targetTransform.Position, 360f, deltaTime);
            MoveUseCase.Move(ref transform.ValueRW, targetTransform.Position, speed.ValueRO.Value, deltaTime);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}