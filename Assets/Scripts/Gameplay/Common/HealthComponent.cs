using System;
using Modules.Pooling;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class HealthComponent
    {
        [SerializeField] private int _health;
        public int CurrentHealth => _health;

        public HealthComponent(int health) => _health = health;

        public void TakeDamage(int damage, IDespawned component)
        {
            if (damage <= 0) return;
            
            _health -= damage;
            if (_health <= 0)
                component.Destroy();
        }
    }
}