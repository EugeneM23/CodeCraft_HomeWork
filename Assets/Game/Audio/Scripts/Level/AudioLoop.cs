using AudioEngine;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// One looped sound that is either on or off: <see cref="Set"/> starts it once and stops it with a fade.
    /// Made once per owner; switching it costs no allocation.
    /// </summary>
    public sealed class AudioLoop
    {
        private readonly string _id;
        private readonly float _fadeOut;
        private AudioEventHandle _handle;

        public AudioLoop(string id, float fadeOut = 0.3f)
        {
            _id = id;
            _fadeOut = fadeOut;
        }

        public bool IsPlaying => _handle.IsValid;

        /// <param name="follow">What the sound follows; null plays it 2D.</param>
        public void Set(bool on, Transform follow)
        {
            if (on)
                Start(follow);
            else
                Stop();
        }

        public void Start(Transform follow)
        {
            if (_handle.IsValid)
                return;

            if (follow != null)
                GameAudio.StartLoop(_id, follow, out _handle);
            else
                GameAudio.StartLoop(_id, out _handle);
        }

        public void Stop() => GameAudio.StopLoop(ref _handle, _fadeOut);

        public void StopNow() => GameAudio.StopLoop(ref _handle, 0f);
    }
}
