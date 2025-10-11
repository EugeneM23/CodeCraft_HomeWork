using UnityEngine;

namespace Gameplay
{
    public class DeathObserver : IInitializeble
    {
        private HealthComponent _healthComponent;
        private Transform _player;

        [Inject]
        private void Construct(HealthComponent healthComponent, Transform player)
        {
            _healthComponent = healthComponent;
            _player = player;
        }

        public void Initialize()
        {
            _healthComponent.OnDeath += OnDeath;
        }

        private void OnDeath()
        {
            _player.gameObject.SetActive(false);
        }
    }
}