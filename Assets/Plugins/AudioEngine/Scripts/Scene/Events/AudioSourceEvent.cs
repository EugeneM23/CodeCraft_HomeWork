using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEditor;
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
                this.ResetState();
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

        protected internal override IEnumerator FadeOut(AnimationCurve curve, float duration)
        {
            float time = 0f;
            float startVolume = _audioSource.volume;

            while (time < duration)
            {
                float t = time / duration;
                float fadeMultiplier = curve?.Evaluate(t) ?? 1f - t;
                _audioSource.volume = startVolume * fadeMultiplier;

                time += Time.deltaTime;
                yield return null;
            }

            _audioSource.volume = 0f;
        }

        private void ResetState()
        {
            _audioSource.volume = this.volume?.Value ?? 1;
            _audioSource.pitch = this.pitch?.Value ?? 1;
            _audioSource.clip = this.clip?.Value;
            _audioSource.reverbZoneMix = reverbZoneMix?.Value ?? 1;

            _audioSource.dopplerLevel = this.dopplerLevel.Value;
            _audioSource.spread = this.spread.Value;
            _audioSource.minDistance = this.minDistance.Value;
            _audioSource.maxDistance = this.maxDistance.Value;
            _audioSource.rolloffMode = this.rolloffMode;
            
            if (this.rolloffMode == AudioRolloffMode.Custom)
                _audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, this.rolloffCurve);

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
            _currentTime = Mathf.Min(_currentTime + deltaTime, this.duration);
            _currentProgress = Mathf.Clamp01(_currentTime / this.duration);
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