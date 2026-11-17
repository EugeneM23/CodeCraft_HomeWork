#if SAVE_AUDIO
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Built in: the clip (an asset reference), the time, playing or paused, volume, pitch, mute. Paused means not
    /// playing with the time past the start.
    /// </summary>
    public sealed class AudioSourceSerializer : ComponentSerializer<AudioSource, AudioSourceSerializer.Data>
    {
        public sealed class Data
        {
            public AudioClip Clip;
            public bool HasClip;
            public float Time;
            public bool Playing;
            public bool Paused;
            public float Volume = 1f;
            public float Pitch = 1f;
            public bool Mute;
        }

        protected override Data Serialize(AudioSource source) => new()
        {
            Clip = source.clip,
            HasClip = source.clip != null,
            // Without a clip (an AudioResource such as a random container, or nothing) Unity warns on every read
            Time = source.clip != null ? source.time : 0f,
            Playing = source.isPlaying,
            Paused = !source.isPlaying && source.clip != null && source.time > 0f,
            Volume = source.volume,
            Pitch = source.pitch,
            Mute = source.mute
        };

        protected override void Deserialize(AudioSource source, Data data)
        {
            source.Stop();
            if (data.Clip != null || !data.HasClip)
                source.clip = data.Clip;

            source.volume = data.Volume;
            source.pitch = data.Pitch;
            source.mute = data.Mute;
            if (source.clip == null || (!data.Playing && !data.Paused))
                return;

            // Play starts from the beginning: the time goes after it
            source.Play();
            source.time = Mathf.Clamp(data.Time, 0f, Mathf.Max(0f, source.clip.length - 0.01f));
            if (data.Paused)
                source.Pause();
        }
    }
}
#endif
