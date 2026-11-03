using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
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

            if (_gameContext.GetEntityWorld().Contains(_entity))
                _gameContext.GetEntityWorld().Remove(_entity);
        }
    }
}