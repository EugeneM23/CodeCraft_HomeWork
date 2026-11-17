using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fires the Attack trigger when the character's weapon goes off.
    /// </summary>
    /// <remarks>
    /// AC_Zombie declares Attack, TakeDamage and Death as triggers and reaches those states only
    /// through AnyState transitions conditioned on them, but nothing in the project ever called
    /// SetTrigger, so the attack animation could not play at all. Death happened to work because
    /// DeathAnimBehaviour plays that state by name and bypasses its trigger.
    ///
    /// The hook is the weapon's own FireEvent: an attack raises it on the weapon, and the character
    /// has none of its own.
    /// </remarks>
    public sealed class CharacterAttackAnimBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AnimatorTrigger _attack = new("Attack");

        private Animator _animator;
        private HeldWeaponFireListener _weaponFire;

        public void Init(IEntity entity)
        {
            _animator = entity.GetAnimator();
            _weaponFire = new HeldWeaponFireListener(entity.GetWeapon(), this.OnFire);
        }

        public void Dispose(IEntity entity) => _weaponFire.Dispose();

        private void OnFire(IEntity weapon) => _attack.Fire(_animator);
    }
}
