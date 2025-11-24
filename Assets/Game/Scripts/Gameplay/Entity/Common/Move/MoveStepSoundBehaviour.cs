using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class MoveStepSoundBehaviour : IEntityInit, IEntityUpdate
    {
        private const float TOLERANCE = 0.1f;
        private readonly AudioClip[] _audioClips;
        private readonly float _stepTime;
        private AudioSource _audioSource;
        private IReactiveVariable<float> _velocity;

        private float _timer;

        public MoveStepSoundBehaviour(AudioClip[] audioClips, float stepTime = 0.35f)
        {
            _audioClips = audioClips;
            _stepTime = stepTime;
        }

        public void Init(in IEntity entity)
        {
            _audioSource = entity.GetAudioSource();
            _velocity = entity.GetVelocity();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_velocity.Value < TOLERANCE)
            {
                _timer = _stepTime;
                return;
            }

            _timer -= deltaTime;

            if (_timer <= 0)
            {
                PlayMoveStep();
                _timer = _stepTime;
            }
        }

        private void PlayMoveStep()
        {
            int randomIndex = Random.Range(0, _audioClips.Length);

            _audioSource.PlayOneShot(_audioClips[randomIndex], 0.5f);
        }
    }
}