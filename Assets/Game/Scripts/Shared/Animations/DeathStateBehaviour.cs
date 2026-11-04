using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes the death state final by clearing what would pull the animator back out of it.
    /// </summary>
    /// <remarks>
    /// The blow that kills also damages, and <see cref="EnemyTakeDamageAnimBehaviour"/> sets the
    /// TakeDamage trigger for it. DeathBehaviour then raises the death event in the same frame and
    /// DeathAnimBehaviour plays Death -- but the trigger is still pending, so on the next
    /// evaluation the AnyState transition fires, the corpse flinches, the flinch exits to Idle,
    /// and IsMoving is still true, so the dead creature walks on the spot.
    ///
    /// Resetting the triggers on entry is enough: nothing sets them again, because DeathBehaviour
    /// disposes the entity's behaviours right after raising the event. IsMoving goes with them so
    /// that nothing can reach Move either.
    /// </remarks>
    public sealed class DeathStateBehaviour : StateMachineBehaviour
    {
        private static readonly int ATTACK = Animator.StringToHash("Attack");
        private static readonly int TAKE_DAMAGE = Animator.StringToHash("TakeDamage");
        private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.ResetTrigger(ATTACK);
            animator.ResetTrigger(TAKE_DAMAGE);
            animator.SetBool(IS_MOVING, false);
        }
    }
}
