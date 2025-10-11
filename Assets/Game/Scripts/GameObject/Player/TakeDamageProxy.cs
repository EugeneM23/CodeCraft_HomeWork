using UnityEngine;

namespace Gameplay
{
    public class TakeDamageProxy : MonoBehaviour, IDamageable
    {
        private HealthComponent _healthComponent;

        private void OnEnable()
        {
            _healthComponent = ServiceLocator.Get<HealthComponent>(PlayerId.HealthComponent);
        }

        public void TakeDamage(int damage) => _healthComponent.TakeDamage(damage);
    }
}