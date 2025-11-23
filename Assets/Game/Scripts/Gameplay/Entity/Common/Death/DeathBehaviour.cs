using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public class DeathBehaviour : IEntityInit, IEntityDispose
    {
        private Health _health;
        private IEntity _entity;

        public void Init(in IEntity entity)
        {
            _entity = entity;
            _health = entity.GetHealth();

            _health.OnHealthEmpty += OnHealthEmpty;
        }

        public void Dispose(in IEntity entity) => _health.OnHealthEmpty -= OnHealthEmpty;

        private void OnHealthEmpty()
        {
            _entity.Disable();

            if (_entity.TryGetDeathAction(out var action))
                action.Invoke();
        }
    }
}