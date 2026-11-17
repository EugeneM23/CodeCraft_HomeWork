using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class SwitchAnimatorUseCase
    {
        public static void Switch(Animator animator, IEntity weapon)
        {
            animator.runtimeAnimatorController = weapon.GetAnimationController();

            // Rebind() resets every layer back to its default state, so it has to happen before
            // Play, not after -- the old order threw the Play away and left the new controller
            // sitting in whatever each layer's default state happens to be.
            animator.Rebind();
            animator.Play("Idle");
            animator.Update(0f);
        }
    }
}