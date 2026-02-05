using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components.AttackDistance;
using Rukhanka;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct CombatAnimationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (target, attackDistance, entityTransform, animParams, indexes) in SystemAPI
                     .Query<RefRO<Target>,
                         RefRO<AttackDistance>,
                         RefRO<LocalTransform>,
                         DynamicBuffer<AnimatorControllerParameterComponent>,
                         AnimatorControllerParameterIndexTableComponent>())
        {
            Entity targetEntity = target.ValueRO.Value;

            // Создаём аспект для работы с аниматором
            var animator = new AnimatorParametersAspect(animParams, indexes);

            // Сбрасываем все параметры
            animator.SetParameterValue("Attack", false);
            animator.SetParameterValue("Walk", false);

            // Если цели нет, стоим на месте (idle)
            if (targetEntity == Entity.Null ||
                !SystemAPI.Exists(targetEntity) ||
                !SystemAPI.HasComponent<LocalTransform>(targetEntity))
            {
                continue;
            }

            // Получаем позицию цели
            var targetTransform = SystemAPI.GetComponentRO<LocalTransform>(targetEntity);

            // Вычисляем дистанцию до цели
            float distance = math.distance(
                entityTransform.ValueRO.Position,
                targetTransform.ValueRO.Position
            );

            // Переключаем анимацию в зависимости от дистанции
            if (distance < attackDistance.ValueRO.Value)
            {
                // В радиусе атаки - атакуем
                animator.SetParameterValue("Attack", true);
            }
            else
            {
                // Вне радиуса - идём к цели
                animator.SetParameterValue("Walk", true);
            }
        }
    }
}

