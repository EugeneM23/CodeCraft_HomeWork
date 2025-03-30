using Modules.Pools;
using UnityEngine;

namespace Gameplay
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D _rigidbody2D;

        private int _damage;
        private DefaultPool<Bullet> _pool;
        private bool _collisionEnable = true;

        private void OnEnable() => _collisionEnable = true;

        public void Construct(int damage, Color color, PhysicsLayer physicsLayer)
        {
            _damage = damage;
            spriteRenderer.color = color;
            gameObject.layer = (int)physicsLayer;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_collisionEnable) return;

            if (collision.gameObject.TryGetComponent<IDamageable>(out IDamageable unit))
            {
                _collisionEnable = false;
                unit.TakeDamage(_damage);
                
                Destroy();
            }
        }

        public void Destroy()
        {
            _pool.Return(this);
        }

        public void SetVelocity(Vector3 fireDirection) => _rigidbody2D.velocity = fireDirection;

        public void SetPull(DefaultPool<Bullet> pool) => _pool = pool;
    }
}