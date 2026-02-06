using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct CalculateDistanceToTargetSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (distance, target, entity) in SystemAPI.Query<RefRW<DistanceToTarget>, RefRO<Target>>()
                     .WithEntityAccess())
        {
            var targetEntity = target.ValueRO.Value;

            if (targetEntity == Entity.Null)
                continue;

            if (!state.EntityManager.Exists(targetEntity))
                continue;

            if (!state.EntityManager.HasComponent<LocalTransform>(targetEntity))
                continue;
            
            var entityPosition = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
            var targetPosition = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value).Position;
            distance.ValueRW.Value = math.distance(targetPosition, entityPosition);
        }
    }
}