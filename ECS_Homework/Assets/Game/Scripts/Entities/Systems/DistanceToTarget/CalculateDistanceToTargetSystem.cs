using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct CalculateDistanceToTargetSystem : ISystem
{
    public static readonly float3 Offset = new(0, 1f, 0);

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
                math.distance(myTransform.ValueRO.Position - Offset, targetTransform.Position) - targetRadius;
        }
    }
}

public partial struct TestDist : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var item in SystemAPI.Query<ProjectileTag, TargetDistance>())
        {
            Debug.Log(item.Item2.Value);
        }
    }
}