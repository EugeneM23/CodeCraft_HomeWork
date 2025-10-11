using UnityEngine;

namespace Gameplay
{
    public class TakeDamageProxy : MonoBehaviour, IDamageable
    {
        private HealthComponent _healthComponent;

        [Inject]
        private void Construct(HealthComponent healthComponent)
        {
            _healthComponent = healthComponent;
        }

        public void TakeDamage(int damage) => _healthComponent.TakeDamage(damage);
    }
}