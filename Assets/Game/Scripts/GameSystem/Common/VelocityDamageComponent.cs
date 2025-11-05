using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class VelocityDamageComponent : MonoBehaviour
    {
        [SerializeField] private float _limitvelocity = 50;
        [SerializeField] private int _damage = 100;

        private Rigidbody2D _rigidbody;

        private void OnEnable() => _rigidbody = GetComponent<Rigidbody2D>();

        private void OnCollisionEnter2D(Collision2D other)
        {
            Vector2 velocity = _rigidbody.linearVelocity;

            if (velocity.magnitude > _limitvelocity)
            {
                if (other.transform.TryGetComponent<Entity>(out var entity))
                {
                    var damageable = entity.GetEntityComponent<IDamageable>();
                    damageable.TakeDamage(_damage);
                }
            }
        }
    }
}