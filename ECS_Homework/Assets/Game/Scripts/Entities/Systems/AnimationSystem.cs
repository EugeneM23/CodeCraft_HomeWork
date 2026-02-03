using Rukhanka;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

public partial struct AnimationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AnimationRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animParams, indexes, request, entity)in SystemAPI
                     .Query<DynamicBuffer<AnimatorControllerParameterComponent>,
                         AnimatorControllerParameterIndexTableComponent, AnimationRequest>().WithEntityAccess())

        {
            var animator = new AnimatorParametersAspect(animParams, indexes);

            animator.SetParameterValue("Attack", false);
            animator.SetParameterValue(request.AnimationName, true);
            ecb.RemoveComponent<AnimationRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}