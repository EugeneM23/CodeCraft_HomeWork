using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public class DeathBehaviour : IEntityInit, IEntityDispose
    {
        private Health _health;
        private GameObject _gameObject;

        public void Init(in IEntity entity)
        {
            _health = entity.GetHealth();
            _gameObject = entity.GetGameObject();

            _health.OnHealthEmpty += OnHealthEmpty;
        }

        public void Dispose(in IEntity entity) => _health.OnHealthEmpty -= OnHealthEmpty;

        private void OnHealthEmpty()
        {
            _gameObject.SetActive(false);
        }
    }
}