using Game.Animation;
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
            animator.SetParameterValue(ACBase.Attack.ToParameterName(), false);
            animator.SetParameterValue(ACBase.Walk.ToParameterName(), false);

            if (targetEntity == Entity.Null)
                continue;

            // Получаем позицию цели
            // Вычисляем дистанцию до цели
            var targetTransform = SystemAPI.GetComponentRO<LocalTransform>(targetEntity);
            var distance = math.distance(entityTransform.ValueRO.Position, targetTransform.ValueRO.Position);

            // Переключаем анимацию в зависимости от дистанции
            if (distance < attackDistance.ValueRO.Value)
                animator.SetParameterValue(ACBase.Attack.ToParameterName(), true);
            else
                animator.SetParameterValue(ACBase.Walk.ToParameterName(), true);
        }
    }
}