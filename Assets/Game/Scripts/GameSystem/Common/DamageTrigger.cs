using System;
using UnityEngine;

namespace Gameplay
{
    public class DamageTrigger : MonoBehaviour
    {
        [SerializeField] private int _damage = 50;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.TryGetComponent<Entity>(out var entity))
            {
                _damage = 50;
                entity.GetEntityComponent<HealthComponent>().TakeDamage(_damage);
            }
        }
    }
}