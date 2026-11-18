using AudioEngine;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The battle music as three loops of one length — calm, horde, peak — started on one dspTime so they stay in
    /// step, each on its own source in the Music group. Each layer plays the first clip of its catalog; a layer
    /// with an empty catalog (or no source) stays silent. <see cref="Tick"/> sets every layer's volume to
    /// master × fade × the layer's weight at the given <see cref="MusicIntensity"/> value. Owned and ticked by
    /// <see cref="AppAudio"/>; plain class, arrays made once, nothing allocated per frame.
    /// </summary>
    public sealed class LayeredMusic
    {
        private const int CALM = 0;
        private const int HORDE = 1;
        private const int PEAK = 2;
        private const int LAYERS = 3;

        /// <summary>Seconds between asking the layers to start and their shared start, so all three make it.</summary>
        /// <summary>Lead time of the scheduled start: room for three streamed clips to buffer before they play together.</summary>
        private const double START_DELAY = 0.3;

        private enum Fade { None, In, Out }

        private readonly AudioSource[] _sources = new AudioSource[LAYERS];
        private readonly MusicCatalog[] _catalogs = new MusicCatalog[LAYERS];

        private Fade _fade;
        private float _fadeLevel;
        private float _fadeTime;
        private bool _started;

        public LayeredMusic(AudioMixerGroup group,
            AudioSource calm, MusicCatalog calmCatalog,
            AudioSource horde, MusicCatalog hordeCatalog,
            AudioSource peak, MusicCatalog peakCatalog)
        {
            SetLayer(CALM, calm, calmCatalog, group);
            SetLayer(HORDE, horde, hordeCatalog, group);
            SetLayer(PEAK, peak, peakCatalog, group);
        }

        /// <summary>True when at least one layer has a source and a clip to play.</summary>
        public bool HasClips
        {
            get
            {
                for (int i = 0; i < LAYERS; i++)
                    if (_sources[i] != null && FirstClip(_catalogs[i]) != null)
                        return true;

                return false;
            }
        }

        /// <summary>True from <see cref="Play"/> until a <see cref="Stop"/> has faded all the way out.</summary>
        public bool IsPlaying => _started;

        /// <summary>0..1: how far the layers have faded in.</summary>
        public float FadeLevel => _fadeLevel;

        /// <summary>
        /// Fades the layers in over <paramref name="fadeTime"/> seconds. Started layers (still fading out) fade
        /// back in where they are; stopped ones start together from their first clip.
        /// </summary>
        public void Play(float fadeTime)
        {
            if (!_started)
            {
                if (!HasClips)
                    return;

                StartLayers();
            }

            _fadeTime = fadeTime;
            _fade = Fade.In;
        }

        /// <summary>Fades the layers out over <paramref name="fadeTime"/> seconds, then stops them.</summary>
        public void Stop(float fadeTime)
        {
            if (!_started)
                return;

            _fadeTime = fadeTime;
            _fade = Fade.Out;
        }

        /// <summary>Advances the fade and sets the layer volumes. Called every frame, also during the pause.</summary>
        public void Tick(float deltaTime, float masterVolume, float intensity)
        {
            if (!_started)
                return;

            float step = _fadeTime > 0f ? deltaTime / _fadeTime : 1f;
            switch (_fade)
            {
                case Fade.In:
                    _fadeLevel = Mathf.Min(1f, _fadeLevel + step);
                    if (_fadeLevel >= 1f)
                        _fade = Fade.None;
                    break;
                case Fade.Out:
                    _fadeLevel = Mathf.Max(0f, _fadeLevel - step);
                    if (_fadeLevel <= 0f)
                    {
                        StopLayers();
                        return;
                    }

                    break;
            }

            float level = Mathf.Clamp01(masterVolume) * _fadeLevel;
            SetVolume(CALM, level * MusicIntensity.CalmWeight(intensity));
            SetVolume(HORDE, level * MusicIntensity.HordeWeight(intensity));
            SetVolume(PEAK, level * MusicIntensity.PeakWeight(intensity));
        }

        private void SetLayer(int layer, AudioSource source, MusicCatalog catalog, AudioMixerGroup group)
        {
            _sources[layer] = source;
            _catalogs[layer] = catalog;
            if (source == null)
                return;

            if (group != null)
                source.outputAudioMixerGroup = group;
            source.ignoreListenerPause = true;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
        }

        // One dspTime for all three: scheduled starts are sample-accurate, Play() on each is not.
        private void StartLayers()
        {
            double start = AudioSettings.dspTime + START_DELAY;
            for (int i = 0; i < LAYERS; i++)
            {
                AudioSource source = _sources[i];
                AudioClip clip = source != null ? FirstClip(_catalogs[i]) : null;
                if (clip == null)
                    continue;

                if (clip.loadState is AudioDataLoadState.Unloaded or AudioDataLoadState.Failed)
                    clip.LoadAudioData();
                source.clip = clip;
                source.volume = 0f;
                source.PlayScheduled(start);
            }

            _fadeLevel = 0f;
            _started = true;
        }

        private void StopLayers()
        {
            for (int i = 0; i < LAYERS; i++)
            {
                AudioSource source = _sources[i];
                if (source == null)
                    continue;

                source.Stop();
                source.volume = 0f;
            }

            _fade = Fade.None;
            _fadeLevel = 0f;
            _started = false;
        }

        private void SetVolume(int layer, float volume)
        {
            AudioSource source = _sources[layer];
            if (source != null && source.clip != null)
                source.volume = volume;
        }

        private static AudioClip FirstClip(MusicCatalog catalog)
        {
            if (catalog == null)
                return null;

            for (int i = 0; i < catalog.Count; i++)
                if (catalog[i] != null)
                    return catalog[i];

            return null;
        }
    }
}
