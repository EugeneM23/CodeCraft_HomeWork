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
        // Sized ahead and only emptied by Remove on resume, so pause after pause reuses its buckets (no growth).
        private readonly Dictionary<Animator, float> _speeds = new(64);

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
