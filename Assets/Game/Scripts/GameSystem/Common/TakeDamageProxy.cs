using UnityEngine;

namespace Gameplay
{
    public class TakeDamageProxy : MonoBehaviour, IDamageable
    {
        private HealthComponent _healthComponent;
        public string _massage;

        [Inject]
        private void Construct(HealthComponent healthComponent, string massage)
        {
            _massage = massage;
            _healthComponent = healthComponent;
        }

        public void TakeDamage(int damage) => _healthComponent.TakeDamage(damage);
    }
}