using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct CalculateDistanceToTargetSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach (var (distance, target, transform) in SystemAPI
                     .Query<RefRW<DistanceToTarget>, RefRO<Target>, RefRO<LocalTransform>>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;
            if (!transformLookup.HasComponent(target.ValueRO.Value)) continue;

            var targetPosition = transformLookup[target.ValueRO.Value].Position;
            distance.ValueRW.Value = math.distance(transform.ValueRO.Position, targetPosition);
        }
    }
}