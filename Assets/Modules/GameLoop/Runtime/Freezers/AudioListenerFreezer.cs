#if GAMELOOP_AUDIO
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// AudioListener.pause while paused: every AudioSource stops, new ones start paused, scheduled sounds wait. Menu
    /// sounds must set AudioSource.ignoreListenerPause. For games without their own audio engine.
    /// </summary>
    public sealed class AudioListenerFreezer : IPauseListener
    {
        private bool _wasPaused;
        private bool _frozen;

        public void OnPause()
        {
            if (_frozen)
                return;

            _wasPaused = AudioListener.pause;
            AudioListener.pause = true;
            _frozen = true;
        }

        public void OnResume()
        {
            if (!_frozen)
                return;

            AudioListener.pause = _wasPaused;
            _frozen = false;
        }
    }
}
#endif
