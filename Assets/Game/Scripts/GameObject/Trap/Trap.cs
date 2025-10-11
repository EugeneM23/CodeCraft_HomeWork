using System;
using UnityEngine;

namespace Gameplay
{
    public class Trap : MonoBehaviour
    {
        [SerializeField] private int _damage;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out IDamageable healthComponent))
                healthComponent.TakeDamage(_damage);
        }
    }
}