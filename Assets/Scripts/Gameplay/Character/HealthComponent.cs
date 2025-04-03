using System;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class HealthComponent
    {
        [SerializeField] private int _health;

        public void TakeDamage(int damage, IDespawned component)
        {
            _health -= damage;
            if (_health <= 0)
                component.Destroy();
        }
    }
}