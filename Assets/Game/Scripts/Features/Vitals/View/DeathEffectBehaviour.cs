using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shows the death effect when the character dies.
    /// </summary>
    /// <remarks>
    /// There is nothing to Play() here, unlike the blood in <see cref="TakeDamageBloodBehaviour"/>.
    /// The volumetric blood is a mesh animated from its own scripts rather than a ParticleSystem,
    /// and it runs the moment it is enabled -- which is also why it sits on the prefab switched
    /// off, so that it does not fire the instant the character spawns. Enabling it is the whole
    /// job.
    ///
    /// The corpse's GameObject outlives this: DeathBehaviour disables the entity and disposes its
    /// behaviours, but never destroys the object, so the effect plays out where the character
    /// fell.
    /// </remarks>
    public sealed class DeathEffectBehaviour : IEntityInit, IEntityDispose
    {
        private readonly GameObject _effect;

        private ISignal<TakeDamageArgs> _deathTakenEvent;

        public DeathEffectBehaviour(GameObject effect) => _effect = effect;

        public void Init(IEntity entity)
        {
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _deathTakenEvent.Subscribe(this.OnDeath);
        }

        public void Dispose(IEntity entity) => _deathTakenEvent.Unsubscribe(this.OnDeath);

        private void OnDeath(TakeDamageArgs _)
        {
            if (_effect != null)
                _effect.SetActive(true);
        }
    }
}
