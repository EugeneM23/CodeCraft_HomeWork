using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class SwitchAnimatorUseCase
    {
        public static void Switch(Animator animator, IEntity weapon)
        {
            animator.runtimeAnimatorController = weapon.GetAnimationController();
            animator.Play("Idle");
            animator.Rebind();
            animator.Update(0f);
        }
    }
}