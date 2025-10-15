using System;
using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class MoveComponent : CompositCondition, IFixedTickable
    {
        [Inject] private readonly Transform _transform;
        [Inject] private CollisionComponent _collision;
        private readonly Rigidbody2D _rigidbody;
        private float _moveSpeed = 5f;
        private Vector2 _direction;

        public MoveComponent(Rigidbody2D rigidbody, float moveSpeed)
        {
            _rigidbody = rigidbody;
            _moveSpeed = moveSpeed;
        }

        public void FixedTick() => Move();

        public void Move()
        {
            if (AndCondition())
                return;
            
            Vector2 normal = _collision.GetHitNormal();
            if (normal == Vector2.zero)
                normal = Vector2.up;

            Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

            float inputX = _direction.x;
            Vector2 moveVelocity = tangent * inputX * _moveSpeed;

            _rigidbody.linearVelocity = new Vector2(moveVelocity.x, _rigidbody.linearVelocity.y);
        }

        public void SetDirection(Vector2 direction) => _direction = direction;

        public void AddGroundMove(Vector3 offset)
        {
            _rigidbody.position += (Vector2)offset;
        }
    }
}