using Modules.Pools;
using UnityEngine;

namespace Gameplay
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private MoveComponent _moveComponent;

        private bool _collisionEnable = true;
        private DefaultPool<Bullet> _pool;
        private Vector3 _moveDirection;
        private int _damage;

        private void OnEnable() => _collisionEnable = true;

        private void Update() => _moveComponent.Move(_moveDirection);

        public void Construct(int damage, Color color, PhysicsLayer physicsLayer)
        {
            _damage = damage;
            spriteRenderer.color = color;
            gameObject.layer = (int)physicsLayer;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_collisionEnable) return;

            if (collision.gameObject.TryGetComponent(out IDamageable unit))
            {
                _collisionEnable = false;
                unit.TakeDamage(_damage);

                Destroy();
            }
        }

        public void Destroy() => _pool.Return(this);

        public void SetPull(DefaultPool<Bullet> pool) => _pool = pool;

        public void SetMoveDirection(Vector3 moveDirection) => _moveDirection = moveDirection;
    }
}