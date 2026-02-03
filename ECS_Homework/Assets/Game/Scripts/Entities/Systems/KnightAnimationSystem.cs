using Rukhanka;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

public partial struct AttackAnimationSystem : ISystem
{
    private FastAnimatorParameter _attackParam;

    public void OnCreate(ref SystemState state)
    {
        _attackParam = new FastAnimatorParameter("Attack");
        state.RequireForUpdate<AttackRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var attackParam = _attackParam;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animParams, indexes, entity)in SystemAPI
                     .Query<DynamicBuffer<AnimatorControllerParameterComponent>, AnimatorControllerParameterIndexTableComponent>()
                     .WithAll<AttackRequest>()
                     .WithEntityAccess())

        {
            Debug.Log("AttackSystem");
            var animator = new AnimatorParametersAspect(animParams, indexes);
            animator.SetParameterValue(attackParam, true);

            ecb.RemoveComponent<AttackRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
