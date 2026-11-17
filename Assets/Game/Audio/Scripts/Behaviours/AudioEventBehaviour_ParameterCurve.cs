using System;
using AudioEngine;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Drives an event's volume and/or pitch from a float bank parameter through curves: the engine loop's pitch
    /// by tank.rpm, the rain's volume by world.rain. Both multiply what the event itself set (its volume and
    /// pitch providers), so the volume tuned in the event asset stays the ceiling.
    ///
    /// The parameter is <see cref="Parameter"/> when it names a bank, else the plain id
    /// <see cref="ParameterId"/> ("tank.rpm"). An unset parameter leaves the event as it is.
    /// </summary>
    [Serializable]
    public sealed class AudioEventBehaviour_ParameterCurve : AudioEventBehaviour
    {
        [SerializeField] private AudioParameterKey _parameter;
        [Tooltip("Full parameter id, \"<bank>.<key>\"; used when Parameter has no bank.")]
        [SerializeField] private string _parameterId;

        [SerializeField] private bool _driveVolume = true;
        [Tooltip("Parameter (x) to volume multiplier (y).")]
        [SerializeField] private AnimationCurve _volume = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField] private bool _drivePitch;
        [Tooltip("Parameter (x) to pitch multiplier (y).")]
        [SerializeField] private AnimationCurve _pitch = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        private float _baseVolume = 1f;
        private float _basePitch = 1f;

        public AudioEventBehaviour_ParameterCurve()
        {
        }

        /// <param name="volume">Null leaves the volume alone.</param>
        /// <param name="pitch">Null leaves the pitch alone.</param>
        public AudioEventBehaviour_ParameterCurve(string parameterId, AnimationCurve volume, AnimationCurve pitch)
        {
            _parameterId = parameterId;
            _driveVolume = volume != null;
            if (volume != null)
                _volume = volume;
            _drivePitch = pitch != null;
            if (pitch != null)
                _pitch = pitch;
        }

        public string ParameterId
        {
            get => _parameterId;
            set => _parameterId = value;
        }

        public AudioParameterKey Parameter
        {
            get => _parameter;
            set => _parameter = value;
        }

        public bool DriveVolume
        {
            get => _driveVolume;
            set => _driveVolume = value;
        }

        public AnimationCurve Volume
        {
            get => _volume;
            set => _volume = value;
        }

        public bool DrivePitch
        {
            get => _drivePitch;
            set => _drivePitch = value;
        }

        public AnimationCurve Pitch
        {
            get => _pitch;
            set => _pitch = value;
        }

        // Called right after the event has set its own volume and pitch for a new round: those are the base.
        public override void OnReset(in AudioSourceEvent evt)
        {
            AudioSource source = evt.Source;
            if (source == null)
                return;

            _baseVolume = source.volume;
            _basePitch = source.pitch;
            Apply(evt);
        }

        public override void OnUpdate(in AudioSourceEvent evt, in float deltaTime) => Apply(evt);

        private void Apply(AudioSourceEvent evt)
        {
            AudioSource source = evt.Source;
            AudioSystem system = evt.System;
            if (source == null || system == null)
                return;

            string id = _parameter;
            if (string.IsNullOrEmpty(id))
                id = _parameterId;
            if (string.IsNullOrEmpty(id) || !system.TryGetFloat(id, out float value))
                return;

            if (_driveVolume && _volume != null)
                source.volume = _baseVolume * Mathf.Max(0f, _volume.Evaluate(value));

            if (_drivePitch && _pitch != null)
                source.pitch = _basePitch * _pitch.Evaluate(value);
        }
    }
}
