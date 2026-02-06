using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Transforms;

public partial struct RotateToTargetWhileAttackingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var manager = state.EntityManager;

        foreach (var (transform, target, entity) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRO<Target>>()
                     .WithAll<IsAttaking>()
                     .WithEntityAccess())
        {
            // Проверяем что цель существует
            if (target.ValueRO.Value == Entity.Null || !manager.Exists(target.ValueRO.Value))
                continue;

            // Получаем позицию цели
            if (!manager.HasComponent<LocalTransform>(target.ValueRO.Value))
                continue;

            var targetTransform = manager.GetComponentData<LocalTransform>(target.ValueRO.Value);

            // Поворачиваем к цели
            RotateUseCase.RotateTowardsPosition(
                ref transform.ValueRW,
                targetTransform.Position,
                rotationSpeedDegrees: 360f, // Скорость поворота - можно настроить
                deltaTime
            );
        }
    }
}