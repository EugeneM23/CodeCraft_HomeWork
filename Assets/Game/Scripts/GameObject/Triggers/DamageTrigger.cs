using System;
using UnityEngine;

namespace Gameplay
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class DamageTrigger : MonoBehaviour
    {
        [SerializeField] private int _damage = 50;
        [SerializeField] private bool _isIntervalDamage;
        [SerializeField] private float _damageInterval = 1f;

        private readonly HashSet<Entity> _entitiesInTrigger = new HashSet<Entity>();

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.TryGetComponent<Entity>(out var entity))
            {
                var health = entity.GetEntityComponent<HealthComponent>();
                health.TakeDamage(_damage);

                if (!_isIntervalDamage) return;

                if (_entitiesInTrigger.Add(entity))
                    StartCoroutine(ApplyPeriodicDamage(entity));
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.gameObject.TryGetComponent<Entity>(out var entity))
            {
                _entitiesInTrigger.Remove(entity); // перестаём бить, когда выходит
            }
        }

        private IEnumerator ApplyPeriodicDamage(Entity entity)
        {
            var health = entity.GetEntityComponent<HealthComponent>();

            while (_entitiesInTrigger.Contains(entity))
            {
                yield return new WaitForSeconds(_damageInterval);
                health.TakeDamage(_damage);
            }
        }
    }
}