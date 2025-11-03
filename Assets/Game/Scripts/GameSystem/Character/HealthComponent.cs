using System;

namespace Gameplay
{
    public class HealthComponent : IDamageable
    {
        public event Action OnDeath;

        private int _currentHealth;
        private int _maxHealth;

        public HealthComponent(int currentHealth)
        {
            _currentHealth = currentHealth;
            _maxHealth = _currentHealth;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;

            _currentHealth -= damage;

            if (_currentHealth <= 0)
                OnDeath?.Invoke();
        }
    }
}