using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct SetAgentDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var radiusLookup = SystemAPI.GetComponentLookup<EntityRadius>(true);

        foreach (var (target, body, myTransform, myRadius) in SystemAPI
                     .Query<RefRO<Target>, RefRW<AgentBody>, RefRO<LocalTransform>, RefRO<EntityRadius>>()
                     .WithNone<IsDead>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;
            if (!transformLookup.HasComponent(target.ValueRO.Value)) continue;

            var targetTransform = transformLookup[target.ValueRO.Value];
            var targetPosition = targetTransform.Position;

            // Получаем радиус цели (если есть)
            float targetRadius = 0f;
            if (radiusLookup.HasComponent(target.ValueRO.Value))
            {
                targetRadius = radiusLookup[target.ValueRO.Value].Value;
            }

            // Вычисляем направление к цели
            var directionToTarget = targetPosition - myTransform.ValueRO.Position;
            var distance = math.length(directionToTarget);

            // Вычисляем точку назначения с учетом радиусов обеих энтити
            // Останавливаемся на расстоянии = сумма радиусов
            var combinedRadius = myRadius.ValueRO.Value + targetRadius;

            float3 destination;
            if (distance > combinedRadius)
            {
                // Нормализуем направление и вычисляем позицию на границе радиусов
                var normalizedDirection = directionToTarget / distance;
                destination = targetPosition - normalizedDirection * combinedRadius;
            }
            else
            {
                // Если уже внутри комбинированного радиуса, остаемся на месте
                destination = myTransform.ValueRO.Position;
            }

            body.ValueRW.SetDestination(destination);
        }
    }
}