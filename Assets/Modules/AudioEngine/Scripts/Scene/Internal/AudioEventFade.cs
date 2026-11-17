using UnityEngine;

namespace AudioEngine
{
    // One running fade-out, ticked by AudioSystem.Update. A plain struct in a pre-allocated list
    // replaces the old coroutines: no garbage per stop, and a paused (disabled) AudioSystem freezes it.
    internal struct AudioEventFade
    {
        public AudioEventBase audioEvent;
        public AnimationCurve curve;
        public float duration;
        public float elapsed;
        public float startVolume;
        public bool dispose;

        public AudioEventFade(AudioEventBase audioEvent, AnimationCurve curve, float duration, float startVolume, bool dispose)
        {
            this.audioEvent = audioEvent;
            this.curve = curve;
            this.duration = duration;
            this.elapsed = 0f;
            this.startVolume = startVolume;
            this.dispose = dispose;
        }
    }
}
