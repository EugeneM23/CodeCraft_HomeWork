using Atomic.Elements;
using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game.Gameplay
{
    public class DeathBehaviour : IEntityInit, IEntityDispose
    {
        private ReactiveInt  _health;
        private GameObject _gameObject;

        public void Init(in IEntity entity)
        {
            _health = entity.GetHealth();
            _gameObject = entity.GetGameObject();

            _health.Subscribe(this.OnHealthChanged);
        }

        private void OnHealthChanged(int health)
        {
            Debug.Log("OnHealthChanged");
            if (health <= 0)
                _gameObject.SetActive(false);
            else
                _gameObject.SetActive(true);
        }

        public void Dispose(in IEntity entity)
        {
            _health.Unsubscribe(this.OnHealthChanged);
        }
    }
}