using Game.Scripts.UI.Entities.Components.AttackDistance;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public partial struct AttackStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (distanceToTarget, attackDistance, animatorRef, entity) in SystemAPI
                     .Query<DistanceToTarget, AttackDistance, AnimatorEntityRefComponent>()
                     .WithAll<IsAttaking>()
                     .WithEntityAccess())
        {
            if (distanceToTarget.Value > attackDistance.Value)
            {
                ecb.AddComponent<IsWalking>(entity);
                ecb.RemoveComponent<IsAttaking>(entity);
                animatorRef.SetAnimationState(ref state, ("IsWalking", true), ("IsAttacking", false));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}