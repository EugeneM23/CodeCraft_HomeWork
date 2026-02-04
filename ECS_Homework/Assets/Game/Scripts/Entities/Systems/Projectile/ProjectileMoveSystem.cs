using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ProjectileMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (transform, target, speed, entity) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRO<Target>, RefRO<Speed>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);

            float distanceSq = math.distancesq(transform.ValueRO.Position, targetTransform.Position);

            if (distanceSq < 2f)
            {
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