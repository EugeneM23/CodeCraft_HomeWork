using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class EnemyAudioInstaller : MonoEntityInstaller
    {
        [SerializeField] private AudioSource _audioSource;

        [SerializeField] private AudioClip[] _deathClips;

        [SerializeField] private AudioClip[] _attackClips;

        [SerializeField] private AudioClip[] _damageClips;

        [SerializeField] private AudioClip _bodyFallClip;

        [SerializeField] private AudioClip _meleeDamageClip;

        [SerializeField] private AudioClip _bulletDamageClip;
        [SerializeField] private AudioClip[] _moveStepClips;

        [SerializeField] private float _stepTime = 1f;

        public override void Install(IEntity entity)
        {
            entity.AddAudioSource(_audioSource);
            entity.AddBehaviour(new HitSoundBehaviour(_damageClips, _audioSource));
            // _attackClips, not _damageClips: enemies grunted in pain whenever they swung, and
            // the attack clips were serialized but never used by anything.
            entity.AddBehaviour(new AttackSoundBehaviour(_attackClips, _audioSource));
            entity.AddBehaviour(new MoveStepSoundBehaviour(_moveStepClips, _stepTime));
            entity.AddBehaviour(new DeathSoundBehaviour(_deathClips));
        }
    }
}