using System;
using UnityEngine;

namespace Gameplay
{
    public class Trap : MonoBehaviour
    {
        [SerializeField] private int _damage;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out Entity entity))
            {
                if (entity.TryGetEntityComponent(out HealthComponent component)) 
                    component.TakeDamage(_damage);
            }
        }
    }
}