using UnityEngine;

namespace Gameplay
{
    public class DeathObserver : IInitializeble
    {
        private readonly HealthComponent _healthComponent;
        private readonly Transform _player;

        public DeathObserver(HealthComponent healthComponent, Transform player)
        {
            _healthComponent = healthComponent;
            _player = player;
        }
        public void Initialize() => _healthComponent.OnDeath += OnDeath;

        private void OnDeath() => _player.gameObject.SetActive(false);
    }
}