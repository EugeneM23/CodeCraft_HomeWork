using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Transforms;

public partial struct SetAgentDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Устанавливаем цель для всех агентов
        foreach (var (target, body, entityTransform) in SystemAPI
                     .Query<RefRO<Target>, RefRW<AgentBody>, RefRO<LocalTransform>>())
        {
            Entity targetEntity = target.ValueRO.Value;

            // Если цели нет, пропускаем
            if (targetEntity == Entity.Null ||
                !SystemAPI.Exists(targetEntity) ||
                !SystemAPI.HasComponent<LocalTransform>(targetEntity))
                continue;

            // Получаем позицию цели
            var targetTransform = SystemAPI.GetComponentRO<LocalTransform>(targetEntity);

            // Устанавливаем точку назначения
            body.ValueRW.SetDestination(targetTransform.ValueRO.Position);
        }
    }
}