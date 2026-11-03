using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Fires the Attack trigger when the enemy's weapon goes off.
    /// </summary>
    /// <remarks>
    /// AC_Zombie declares Attack, TakeDamage and Death as triggers and reaches those states only
    /// through AnyState transitions conditioned on them, but nothing in the project ever called
    /// SetTrigger, so the attack animation could not play at all. Death happened to work because
    /// DeathAnimBehaviour plays that state by name and bypasses its trigger.
    ///
    /// The hook is the weapon's own FireEvent, which MeleeWeaponInstaller raises, and which is the
    /// same signal CharacterShakeController listens to. Going through the weapon rather than the
    /// enemy matters: the enemy's own FireEvent value is never invoked by anything.
    /// </remarks>
    public sealed class EnemyAttackAnimBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int Attack = Animator.StringToHash("Attack");

        private Animator _animator;
        private IReactiveVariable<IEntity> _weapon;
        private IEntity _current;

        public void Init(IEntity entity)
        {
            _animator = entity.GetAnimator();
            _weapon = entity.GetWeapon();

            // Observe invokes straight away, so the weapon held at install time is picked up too.
            _weapon.Observe(this.OnWeaponChanged);
        }

        public void Dispose(IEntity entity)
        {
            _weapon.Unsubscribe(this.OnWeaponChanged);
            this.UnsubscribeWeapon();
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            this.UnsubscribeWeapon();

            _current = weapon;

            if (_current != null && _current.TryGetFireEvent(out IEvent fire))
                fire.Subscribe(this.OnFire);
        }

        private void UnsubscribeWeapon()
        {
            if (_current != null && _current.TryGetFireEvent(out IEvent fire))
                fire.Unsubscribe(this.OnFire);

            _current = null;
        }

        private void OnFire() => _animator.SetTrigger(Attack);
    }
}
