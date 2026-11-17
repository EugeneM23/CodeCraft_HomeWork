using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>Plays a clip in edit mode through a hidden AudioSource that never reaches a scene.</summary>
    internal static class AudioClipPreview
    {
        private const string OBJECT_NAME = "AudioClipPreview";

        private static AudioSource s_source;

        public static AudioClip Playing => s_source != null && s_source.isPlaying ? s_source.clip : null;

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null)
                return;

            AudioSource source = Source();
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        public static void Stop()
        {
            if (s_source != null)
                s_source.Stop();
        }

        public static void Dispose()
        {
            if (s_source == null)
                return;

            Object.DestroyImmediate(s_source.gameObject);
            s_source = null;
        }

        private static AudioSource Source()
        {
            if (s_source != null)
                return s_source;

            GameObject go = new GameObject(OBJECT_NAME) { hideFlags = HideFlags.HideAndDontSave };
            s_source = go.AddComponent<AudioSource>();
            s_source.playOnAwake = false;
            s_source.spatialBlend = 0f;
            return s_source;
        }
    }
}
