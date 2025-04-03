using System;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public class HealthComponent
    {
        [SerializeField] private int _health;

        public void TakeDamage(int damage, Action<GameObject> onDespawn, GameObject gameObject)
        {
            _health -= damage;
            if (_health <= 0)
                onDespawn?.Invoke(gameObject);
        }
    }
}