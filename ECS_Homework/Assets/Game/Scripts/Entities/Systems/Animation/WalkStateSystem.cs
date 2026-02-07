using Game.Scripts.UI.Entities.Components.AttackDistance;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;

public partial struct WalkStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (distance, attackDistance, animatorRef, entity) in SystemAPI
                     .Query<TargetDistance, AttackDistance, AnimatorEntityRefComponent>()
                     .WithAll<IsWalking>()
                     .WithEntityAccess())
        {
            if (distance.Value < attackDistance.Value)
            {
                ecb.AddComponent<IsAttaking>(entity);
                ecb.RemoveComponent<IsWalking>(entity);
                animatorRef.SetAnimationState(ref state, ("IsAttacking", true), ("IsWalking", false));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}