using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Plays an attack clip when the weapon in hand goes off.
    /// </summary>
    /// <remarks>
    /// It used to subscribe to the FireEvent of the entity it sits on — the enemy or the ridden
    /// Regax. Nothing ever raised that one: an attack runs the weapon's FireAction, and the weapon
    /// raises its own FireEvent (MeleeWeaponInstaller, RangeWeaponFireAction). So the attack clips
    /// set on Enemy, Zombie, Regax and Regax_Mount never played.
    /// </remarks>
    public class AttackSoundBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AudioClip[] _attackClips;
        private readonly AudioSource _audioSource;

        private HeldWeaponFireListener _weaponFire;

        public AttackSoundBehaviour(AudioClip[] attackClips, AudioSource audioSource)
        {
            _attackClips = attackClips;
            _audioSource = audioSource;
        }

        public void Init(IEntity entity) =>
            _weaponFire = new HeldWeaponFireListener(entity.GetWeapon(), this.PlaySound);

        public void Dispose(IEntity entity) => _weaponFire.Dispose();

        private void PlaySound(IEntity weapon)
        {
            // A prefab with an empty clip list used to throw IndexOutOfRangeException on the
            // first attack: Random.Range(0, 0) is 0 and the array has no element 0.
            if (_audioSource == null || _attackClips == null || _attackClips.Length == 0)
                return;

            int range = Random.Range(0, _attackClips.Length);
            _audioSource.PlayOneShot(_attackClips[range]);
        }
    }
}
