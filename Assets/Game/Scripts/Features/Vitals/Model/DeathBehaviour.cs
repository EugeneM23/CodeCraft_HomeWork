using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DeathBehaviour : IEntityInit, IEntityDispose
    {
        private Health _health;
        private IEntity _entity;

        private readonly GameContext _gameContext;

        public DeathBehaviour(GameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Init(IEntity entity)
        {
            _entity = entity;
            _health = entity.GetHealth();

            _health.OnHealthEmpty += OnHealthEmpty;
        }

        public void Dispose(IEntity entity) => _health.OnHealthEmpty -= OnHealthEmpty;

        private void OnHealthEmpty()
        {
            _entity.Disable();

            if (_entity.TryGetDeathTakenEvent(out var @event))
                @event.Invoke(new TakeDamageArgs(_entity));

            if (_entity.GetGameObject().TryGetComponent(out CapsuleCollider collider))
                collider.enabled = false;

            // Disable only stops the ticks, and EntityWorld.Remove just drops the entity from the
            // world dictionary — neither runs IEntityDispose. Without this every behaviour on the
            // corpse stayed subscribed to its Health, its events and the context: the blood, sound
            // and buff behaviours kept the dead entity reachable for the rest of the process.
            // The death event is invoked above, before the handlers are released.
            // Dead characters are never returned to the pool, so there is no instance to reuse.
            _entity.Dispose();

            if (_gameContext.GetEntityWorld().Contains(_entity))
                _gameContext.GetEntityWorld().Remove(_entity);
        }
    }
}