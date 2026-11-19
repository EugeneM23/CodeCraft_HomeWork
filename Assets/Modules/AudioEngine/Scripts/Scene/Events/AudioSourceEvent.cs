using System;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Audio;

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable UnassignedField.Local

namespace AudioEngine
{
    [CreateAssetMenu(
        fileName = "Audio Event",
        menuName = "AudioEngine/Scene/New Audio Event"
    )]
    public class AudioSourceEvent : AudioEventBase
    {
        public AudioSystem System => _audioSystem;
        
        public AudioSource Source => _audioSource;

        public float CurrentTime => _currentTime;
        
        public float CurrentProgress => _currentProgress;

        [Title("Main")]
        [SerializeField]
        private float duration;

        [SerializeField]
        private bool loop;

        [SuffixLabel("2D — 3D")]
        [SerializeField, Range(0, 1)]
        private float spartialBlend;

        [SerializeField]
        private AudioMixerGroup output;

        [Title("Audio")]
        [SerializeReference]
        private IClipProvider clip = new ClipConst();

        [SerializeReference, Space]
        private IFloatProvider pitch = new FloatConst();

        [SerializeReference, Space]
        private IFloatProvider volume = new FloatConst();

        [SerializeReference, Space]
        private IFloatProvider reverbZoneMix = new FloatConst(1);

        [FoldoutGroup("3D")]
        [SerializeField, Space]
        private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [FoldoutGroup("3D")]
        [SerializeField, ShowIf(nameof(rolloffMode), AudioRolloffMode.Custom)]
        private AnimationCurve rolloffCurve;

        [FoldoutGroup("3D")]
        [SerializeReference, Space]
        private IFloatProvider minDistance = new FloatConst(1);

        [FoldoutGroup("3D")]
        [SerializeReference]
        private IFloatProvider maxDistance = new FloatConst(500);

        [FoldoutGroup("3D")]
        [SerializeReference]
        private IFloatProvider dopplerLevel = new FloatConst(0.8f);

        [FoldoutGroup("3D")]
        [SerializeReference]
        private IFloatProvider spread = new FloatConst(0);

        [SerializeField]
        private BehaviourInfo[] behaviours;

        [SerializeField, Space]
        private ActionInfo[] actions;

        private AudioSource _audioSource;
        private Transform _audioSourceTransform;

        private float _duration;
        private float _currentTime;
        private float _currentProgress;

        protected internal override Vector3 Position
        {
            get { return _position; }
            set
            {
                _position = value;
                if (_audioSourceTransform != null) _audioSourceTransform.position = value;
            }
        }

        protected internal override Quaternion Rotation
        {
            get { return _rotation; }
            set
            {
                this._rotation = value;
                if (_audioSourceTransform != null) _audioSourceTransform.rotation = value;
            }
        }

        protected internal override void OnStart()
        {
            _audioSource = _audioSystem.TakeAudioSource();

            _audioSource.name = this._identifier;
            _audioSource.outputAudioMixerGroup = this.output;
            _audioSource.loop = this.loop;
            _audioSource.spatialBlend = this.spartialBlend;

            _audioSourceTransform = _audioSource.transform;
            _audioSourceTransform.position = _position;
            _audioSourceTransform.rotation = _rotation;

            this.StartBehaviours();
            this.ResetState();

            _audioSource.Play();
        }

        protected internal override void OnStop()
        {
            this.StopBehaviours();
            _audioSystem.ReleaseAudioSource(_audioSource);
            _audioSource = null;
        }

        protected internal override void OnUpdate(float deltaTime)
        {
            this.StartUpdate(deltaTime);
            this.ProcessUpdate(deltaTime);
            this.FinishUpdate();
        }

        private void FinishUpdate()
        {
            if (_currentProgress < 1)
                return;

            if (this.loop)
            {
                // A looped event without a clip (an empty stub) would otherwise restart every frame.
                if (_duration > 0)
                    this.ResetState();
            }
            else
                this.Complete();
        }

        protected internal override void OnPause()
        {
            if (_audioSource.isPlaying) 
                _audioSource.Pause();
        }

        protected internal override void OnResume()
        {
            if (!_audioSource.isPlaying)
                _audioSource.UnPause();
        }

        protected internal override float FadeVolume
        {
            get => _audioSource != null ? _audioSource.volume : 0f;
            set
            {
                if (_audioSource != null)
                    _audioSource.volume = value;
            }
        }

        /// <summary>0 most important, 256 least (Unity's AudioSource.priority).</summary>
        private int PriorityFor(AudioClip clip)
        {
            if (this.spartialBlend < 0.5f)
                return 16;
            if (this.loop)
                return 48;
            float length = clip != null ? clip.length : 0f;
            return length >= 1.5f ? 64 : length >= 0.5f ? 128 : 200;
        }

        private void ResetState()
        {
            _audioSource.volume = this.volume?.Value ?? 1;
            _audioSource.pitch = this.pitch?.Value ?? 1;
            // Reassigning the clip of a playing source restarts it with a click: keep the same clip as is,
            // and restart only when a random provider picked another one for the next loop round.
            AudioClip nextClip = this.clip?.Value;
            if (_audioSource.clip != nextClip)
            {
                _audioSource.clip = nextClip;
                if (_audioSource.enabled && _currentTime > 0)
                    _audioSource.Play();
            }
            _audioSource.reverbZoneMix = reverbZoneMix?.Value ?? 1;

            // Who keeps playing when more sounds than the real voices are on (game fix): every source used to sit
            // at the default 128, so Unity cut whichever it liked — a cannon's tail, the engine — under a burst of
            // machine-gun shots. Loops and flat (2D: music, UI) sounds first, long one-shots (shots, blasts) next,
            // short chatter last.
            _audioSource.priority = PriorityFor(nextClip);

            _audioSource.dopplerLevel = this.dopplerLevel.Value;
            _audioSource.spread = this.spread.Value;
            _audioSource.minDistance = this.minDistance.Value;
            _audioSource.maxDistance = this.maxDistance.Value;
            _audioSource.rolloffMode = this.rolloffMode;
            
            if (this.rolloffMode == AudioRolloffMode.Custom)
                _audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, this.rolloffCurve);

            // Duration 0 means "as long as the clip": an event with a freshly dropped clip and no duration
            // used to finish on its first frame and cut the sound off.
            AudioClip playingClip = _audioSource.clip;
            float pitchAbs = Mathf.Abs(_audioSource.pitch);
            _duration = this.duration > 0 || playingClip == null
                ? this.duration
                : playingClip.length / (pitchAbs > 0.01f ? pitchAbs : 1f);

            _currentTime = 0;
            _currentProgress = 0;

            this.ResetActions();
            this.ResetBehaviours();
        }

        private void ResetBehaviours()
        {
            for (int i = 0, count = this.behaviours.Length; i < count; i++)
            {
                BehaviourInfo behaviour = this.behaviours[i];
                behaviour.value.OnReset(this);
            }
        }

        private void ResetActions()
        {
            for (int i = 0, count = this.actions.Length; i < count; i++)
            {
                ActionInfo action = this.actions[i];
                action.passed = false;
            }
        }

        private void StartUpdate(float deltaTime)
        {
            _currentTime = Mathf.Min(_currentTime + deltaTime, _duration);
            _currentProgress = _duration > 0 ? Mathf.Clamp01(_currentTime / _duration) : 1f;
        }

        private void ProcessUpdate(float deltaTime)
        {
            //Process actions:
            for (int i = 0, count = this.actions.Length; i < count; i++)
            {
                ActionInfo action = this.actions[i];

                if (action.mute)
                    continue;

                if (!action.passed && _currentTime >= action.time)
                {
                    action.value.Invoke(this);
                    action.passed = true;
                }
            }

            //Process behaviours:
            for (int i = 0, count = this.behaviours.Length; i < count; i++)
            {
                BehaviourInfo behaviour = this.behaviours[i];

                if (behaviour.mute)
                    continue;

                if (behaviour.fullTime || _currentTime >= behaviour.startTime && _currentTime <= behaviour.endTime)
                    behaviour.value.OnUpdate(this, in deltaTime);
            }
        }

        private void StartBehaviours()
        {
            for (int i = 0, count = this.behaviours.Length; i < count; i++)
            {
                BehaviourInfo behaviour = this.behaviours[i];
                behaviour.value.OnStart(this);
            }
        }

        private void StopBehaviours()
        {
            for (int i = 0, count = this.behaviours.Length; i < count; i++)
            {
                BehaviourInfo behaviour = this.behaviours[i];
                behaviour.value.OnStop(this);
            }
        }

        [Serializable]
        private sealed class BehaviourInfo
        {
            [SerializeField]
            public bool fullTime = true;

            [SerializeField, HideIf(nameof(fullTime))]
            public float startTime;

            [SerializeField, HideIf(nameof(fullTime))]
            public float endTime;

            [SerializeReference]
            public IAudioEventBehaviour value;

            [SerializeField, Space]
            public bool mute;
        }

        [Serializable]
        private sealed class ActionInfo
        {
            [SerializeField]
            public float time;

            [SerializeReference]
            public IAudioEventAction value;

            [SerializeField, Space]
            public bool mute;

            internal bool passed;
        }

#if UNITY_EDITOR
        [Title("Tools")]
        [Button("Assign Duration From Clip")]
        [GUIColor(0, 1, 0)]
        private void AssignDurationFromClip()
        {
            this.duration = this.clip?.MaxLength ?? 0;
            AssetDatabase.SaveAssets();
        }
#endif
    }
}