using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Plays the attack animation when the held weapon goes off: Fire on layer 1 for a range
    /// weapon, Melee on layer 2 for a melee one.
    /// </summary>
    /// <remarks>
    /// This used to sit inside CharacterFireAction, right after it fired the weapon. Both weapon
    /// kinds raise their FireEvent from that same fire call (RangeWeaponFireAction and the melee
    /// InlineAction), so listening here plays the animation at the same moment and leaves the
    /// action with only the attack itself.
    ///
    /// Layers 1 and 2 are AC_BaseCharacter's Fire and Melee, and only the player's controller has
    /// them: AC_Zombie has two layers, AC_Rapax one. Playing a layer that is not there logs
    /// "Invalid Layer Index", so the layer count is checked first. Other characters get their attack
    /// animation from CharacterAttackAnimBehaviour's trigger instead.
    /// </remarks>
    public sealed class PlayerCharacterAttackAnimBehaviour : IEntityInit, IEntityDispose
    {
        private const int FIRE_LAYER = 1;
        private const int MELEE_LAYER = 2;

        private static readonly int Fire = Animator.StringToHash("Fire");
        private static readonly int Melee = Animator.StringToHash("Melee");

        private Animator _animator;
        private HeldWeaponFireListener _weaponFire;

        public void Init(IEntity entity)
        {
            _animator = entity.GetAnimator();
            _weaponFire = new HeldWeaponFireListener(entity.GetWeapon(), this.OnFire);
        }

        public void Dispose(IEntity entity) => _weaponFire.Dispose();

        private void OnFire(IEntity weapon)
        {
            if (weapon.HasRangeWeaponTag())
                this.PlayOnLayer(Fire, FIRE_LAYER);

            if (weapon.HasMeleeWeaponTag())
                this.PlayOnLayer(Melee, MELEE_LAYER);
        }

        private void PlayOnLayer(int state, int layer)
        {
            if (layer < _animator.layerCount)
                _animator.Play(state, layer);
        }
    }
}
