using System;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class Bullet : MonoBehaviour, IDespawned
    {
        private Action<GameObject> OnDespawn;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private MoveComponent _moveComponent;

        private bool _collisionEnable = true;
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

            Destroy();
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
        public void SetMoveDirection(Vector3 moveDirection)
        {
            _moveDirection = moveDirection;
        }

        public void Destroy()
        {
            OnDespawn?.Invoke(this.gameObject);
        }

        public void SetDespawnCallBack(Action<GameObject> callback)
        {
            OnDespawn = callback;
        }
    }
}