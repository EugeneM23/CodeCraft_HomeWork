using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

namespace Gameplay
{
    public class HealthComponent : IDamageable
    {
        public event Action OnDeath;

        [Inject] private List<IAction> _actions;
        private int _currentHealth;
        private int _maxHealth;

        public interface IAction
        {
            void Invoke();
        }

        public HealthComponent(int currentHealth)
        {
            _currentHealth = currentHealth;
            _maxHealth = _currentHealth;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;

            _currentHealth -= damage;

            foreach (var item in _actions)
                item.Invoke();

            if (_currentHealth <= 0)
                OnDeath?.Invoke();
        }
    }
}