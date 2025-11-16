using Atomic.Elements;
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

            _health.OnHealthChanged += OnHealthChanged;
        }

        public void Dispose(in IEntity entity) => _health.OnHealthChanged -= OnHealthChanged;

        private void OnHealthChanged(int health)
        {
            _gameObject.SetActive(health > 0);
        }
    }
}