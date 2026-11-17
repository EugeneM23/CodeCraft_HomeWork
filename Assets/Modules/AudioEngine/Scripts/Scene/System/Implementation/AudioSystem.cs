using Sirenix.OdinInspector;
using UnityEngine;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

namespace AudioEngine
{
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("Audio/Audio System", -100)]
    public sealed partial class AudioSystem : MonoBehaviour, IAudioSystem
    {
        [SerializeField]
        private bool initOnAwake = true;

        [Tooltip("AudioSources created up front, so the first shots of a fight do not instantiate them.")]
        [SerializeField, Min(0)]
        private int _initialSources = 16;

        [Tooltip("Instances of every poolable event created when a bank loads, so a fight does not instantiate them.")]
        [SerializeField, Min(0)]
        private int _prewarmPerEvent = 1;

        [FoldoutGroup("Pools")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private AudioSourcePool audioSourcePool;

        [FoldoutGroup("Pools")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private AudioEventPool audioEventPool;

        public void Initialize()
        {
            this.audioSourcePool = new AudioSourcePool(this.transform, _initialSources);
            this.audioEventPool = new AudioEventPool(this);
            this.LoadInitialBanks();
        }

        [Button, GUIColor(1f, 0.83f, 0f), HideInEditorMode]
        public void Pause()
        {
            this.enabled = false;

            foreach (AudioEventBase playingEvent in _playingEvents)
                playingEvent.OnPause();

            this.PauseFadingEvents();
        }

        [Button, GUIColor(1f, 0.83f, 0f), HideInEditorMode]
        public void Resume()
        {
            this.enabled = true;

            foreach (AudioEventBase playingEvent in _playingEvents)
                playingEvent.OnResume();

            this.ResumeFadingEvents();
        }

        internal AudioSource TakeAudioSource()
        {
            return this.audioSourcePool.Take();
        }

        internal void ReleaseAudioSource(AudioSource audioSource)
        {
            this.audioSourcePool.Release(audioSource);
        }

        // Update() does not run in EditMode tests. Like Unity, it does nothing while the system is disabled (paused).
        internal void TickForTests(float deltaTime)
        {
            if (this.enabled)
                this.Tick(deltaTime);
        }

        private void Awake()
        {
            if (this.initOnAwake) this.Initialize();
        }

        private void Update()
        {
            this.Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            this.UpdatePivotEvents();
            this.UpdatePlayingEvents(deltaTime);
            this.UpdateFades(deltaTime);
            this.UpdateMaxFrequencyEvents(deltaTime);
        }

        private void OnDestroy()
        {
            _fades.Clear();
            this.audioSourcePool?.Dispose();
            this.audioEventPool?.Dispose();
        }
    }
}
