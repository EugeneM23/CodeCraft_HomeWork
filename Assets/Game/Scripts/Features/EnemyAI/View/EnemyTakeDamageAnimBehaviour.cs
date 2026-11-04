using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fires the TakeDamage trigger so the enemy flinches when it is hit.
    /// </summary>
    /// <remarks>
    /// Same gap as the attack animation: AC_Zombie reaches its TakeDamage state only through an
    /// AnyState transition conditioned on that trigger, and nothing ever set it.
    /// </remarks>
    public sealed class EnemyTakeDamageAnimBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AnimatorTrigger _takeDamage = new("TakeDamage");

        private Animator _animator;
        private Health _health;
        private IEvent<TakeDamageArgs> _damageTakenEvent;

        public void Init(IEntity entity)
        {
            _animator = entity.GetAnimator();
            _health = entity.GetHealth();
            _damageTakenEvent = entity.GetDamageTakenEvent();

            _damageTakenEvent.Subscribe(this.OnDamageTaken);
        }

        public void Dispose(IEntity entity) => _damageTakenEvent.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs _)
        {
            // The killing blow raises this event as well, and DeathAnimBehaviour plays the Death
            // state right after it. Flinching here would pull the enemy straight back out of its
            // death animation, because the AnyState transition to TakeDamage can fire from Death too.
            if (!_health.Exists())
                return;

            _takeDamage.Fire(_animator);
        }
    }
}
