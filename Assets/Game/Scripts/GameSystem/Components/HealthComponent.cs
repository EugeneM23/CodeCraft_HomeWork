using System;
using UnityEngine;

namespace Gameplay
{
    public class HealthComponent
    {
        public event Action OnDeath;

        private int _maxhealth;
        private int _currentHealth;
        private bool _isAlive;

        public HealthComponent(int maxhealth)
        {
            _maxhealth = maxhealth;
            _currentHealth = _maxhealth;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            if (_currentHealth <= 0)
            {
                _isAlive = false;
                OnDeath?.Invoke();
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            _currentHealth = Mathf.Min(_maxhealth, _currentHealth + amount);
        }

        public bool IsDead() => _currentHealth <= 0;
    }
}