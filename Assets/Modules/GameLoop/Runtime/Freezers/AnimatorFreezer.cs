#if GAMELOOP_ANIMATION
using System.Collections.Generic;
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Stops every active Animator while paused (speed 0, the old speed back on resume). Animators on unscaled time
    /// belong to menus and keep going.
    /// </summary>
    public sealed class AnimatorFreezer : ComponentFreezer<Animator>
    {
        private readonly Dictionary<Animator, float> _speeds = new();

        protected override bool Freeze(Animator animator)
        {
            if (!animator.isActiveAndEnabled || animator.updateMode == AnimatorUpdateMode.UnscaledTime)
                return false;

            _speeds[animator] = animator.speed;
            animator.speed = 0f;
            return true;
        }

        protected override void Thaw(Animator animator)
        {
            if (_speeds.Remove(animator, out float speed))
                animator.speed = speed;
        }
    }
}
#endif
