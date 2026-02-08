using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct CalculateDistanceToTargetSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var radiusLookup = SystemAPI.GetComponentLookup<EntityRadius>(true);

        foreach (var (target, myTransform, distanceToTarget) in SystemAPI
                     .Query<RefRO<Target>, RefRO<LocalTransform>, RefRW<TargetDistance>>()
                     .WithNone<IsDead>())
        {
            radiusLookup.Update(ref state);
            transformLookup.Update(ref state);

            if (!radiusLookup.HasComponent(target.ValueRO.Value)) continue;

            var targetTransform = transformLookup[target.ValueRO.Value];
            float targetRadius = radiusLookup[target.ValueRO.Value].Value;

            distanceToTarget.ValueRW.Value =
                math.distance(myTransform.ValueRO.Position, targetTransform.Position) - targetRadius;
        }
    }
}