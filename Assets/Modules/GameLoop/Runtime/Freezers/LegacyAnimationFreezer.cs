#if GAMELOOP_ANIMATION
using System.Collections.Generic;
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Stops every active legacy Animation component while paused: each of its states gets speed 0, the old speeds
    /// come back on resume. Not-yet-playing states too — a clip that plays automatically on a scene loaded during the
    /// pause starts standing. Switching the component off is not used: that may restart a clip.
    /// </summary>
    public sealed class LegacyAnimationFreezer : ComponentFreezer<Animation>
    {
        private readonly Dictionary<AnimationState, float> _speeds = new();

        protected override bool Freeze(Animation animation)
        {
            if (!animation.isActiveAndEnabled)
                return false;

            foreach (AnimationState state in animation)
            {
                _speeds[state] = state.speed;
                state.speed = 0f;
            }

            return true;
        }

        protected override void Thaw(Animation animation)
        {
            foreach (AnimationState state in animation)
            {
                if (_speeds.Remove(state, out float speed))
                    state.speed = speed;
            }
        }
    }
}
#endif
