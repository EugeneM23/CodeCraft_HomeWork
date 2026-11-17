#if GAMELOOP_PARTICLES
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Pauses every playing particle system while paused and plays on exactly those on resume. Systems on unscaled
    /// time (useUnscaledTime) belong to menus and keep going.
    /// </summary>
    public sealed class ParticleSystemFreezer : ComponentFreezer<ParticleSystem>
    {
        protected override bool Freeze(ParticleSystem system)
        {
            if (!system.isPlaying || system.main.useUnscaledTime)
                return false;

            // Each system of a nested effect is found on its own
            system.Pause(false);
            return true;
        }

        protected override void Thaw(ParticleSystem system)
        {
            if (system.isPaused)
                system.Play(false);
        }
    }
}
#endif
