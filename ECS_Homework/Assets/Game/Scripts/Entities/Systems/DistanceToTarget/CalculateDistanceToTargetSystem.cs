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

        foreach (var (target, myTransform, myRadius, distanceToTarget) in SystemAPI
                     .Query<RefRO<Target>, RefRO<LocalTransform>, RefRO<EntityRadius>, RefRW<DistanceToTarget>>())
        {
            if (target.ValueRO.Value == Entity.Null)
            {
                distanceToTarget.ValueRW.Value = float.MaxValue;
                continue;
            }

            if (!transformLookup.HasComponent(target.ValueRO.Value))
            {
                distanceToTarget.ValueRW.Value = float.MaxValue;
                continue;
            }

            var targetTransform = transformLookup[target.ValueRO.Value];
            
            // Получаем радиус цели (если есть)
            float targetRadius = 0f;
            if (radiusLookup.HasComponent(target.ValueRO.Value))
            {
                targetRadius = radiusLookup[target.ValueRO.Value].Value;
            }

            // Вычисляем расстояние между центрами
            var centerDistance = math.distance(myTransform.ValueRO.Position, targetTransform.Position);
            
            // Вычитаем сумму радиусов, чтобы получить реальное расстояние между краями
            var combinedRadius = myRadius.ValueRO.Value + targetRadius;
            distanceToTarget.ValueRW.Value = math.max(0f, centerDistance - combinedRadius);
        }
    }
}