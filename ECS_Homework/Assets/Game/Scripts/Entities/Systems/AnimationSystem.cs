using System;
using Rukhanka;
using Unity.Entities;
using Unity.Collections;

public partial struct AnimationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AnimationEventRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animParams, indexes, animationEvent, entity)in SystemAPI
                     .Query<DynamicBuffer<AnimatorControllerParameterComponent>,
                         AnimatorControllerParameterIndexTableComponent, AnimationEventRequest>().WithEntityAccess())

        {
            var animator = new AnimatorParametersAspect(animParams, indexes);

            animator.SetParameterValue("Attack", false);
            animator.SetParameterValue(animationEvent.Parameter, true);

            ecb.RemoveComponent<AnimationEventRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}