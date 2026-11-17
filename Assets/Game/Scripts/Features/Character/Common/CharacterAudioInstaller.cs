using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The sounds of every body except the player's: Enemy, Zombie, Regax, Regax_Mount. The player
    /// has PlayerCharacterAudioInstaller.
    /// </summary>
    public sealed class CharacterAudioInstaller : MonoEntityInstaller
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
            entity.AddBehaviour(new DamageSoundBehaviour(_damageClips, _audioSource));
            // _attackClips, not _damageClips: enemies grunted in pain whenever they swung, and
            // the attack clips were serialized but never used by anything.
            entity.AddBehaviour(new AttackSoundBehaviour(_attackClips, _audioSource));
            entity.AddBehaviour(new MoveStepSoundBehaviour(_moveStepClips, _stepTime));
            entity.AddBehaviour(new DeathSoundBehaviour(_deathClips));
        }
    }
}