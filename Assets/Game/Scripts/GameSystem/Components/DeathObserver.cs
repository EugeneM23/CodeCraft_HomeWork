using UnityEngine;

namespace Gameplay
{
    public class DeathObserver : IInitializeble
    {
        private HealthComponent _healthComponent;
        private Transform _player;

        public DeathObserver(Transform player)
        {
            _player = player;
        }

        public void Initialize()
        {
            ServiceLocator.Get<HealthComponent>(PlayerId.HealthComponent).OnDeath += OnDeath;
        }

        private void OnDeath()
        {
            _player.gameObject.SetActive(false);
        }
    }
}