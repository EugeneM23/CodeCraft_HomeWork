using Rukhanka;
using Unity.Collections;
using Unity.Entities;

public partial struct ProcessAnimationRequestSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AnimationEventRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animParams, indexes, animationEvent, entity) in SystemAPI
                     .Query<DynamicBuffer<AnimatorControllerParameterComponent>,
                         AnimatorControllerParameterIndexTableComponent, 
                         AnimationEventRequest>()
                     .WithEntityAccess())
        {
            var animator = new AnimatorParametersAspect(animParams, indexes);

            // Сбрасываем боевые анимации
            animator.SetParameterValue("Attack", false);
            animator.SetParameterValue("Walk", false);

            // Устанавливаем запрошенную анимацию
            animator.SetParameterValue(animationEvent.Parameter, true);

            // Удаляем обработанный запрос
            ecb.RemoveComponent<AnimationEventRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}