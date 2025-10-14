using System;
using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class MoveComponent : CompositCondition, IFixedTickable, IMovabele
    {
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
            {
                Vector2 velocity = _rigidbody.linearVelocity;
                velocity.x = 0;
                _rigidbody.linearVelocity = velocity;
                return;
            }

            _rigidbody.linearVelocity = new Vector2(_direction.x * _moveSpeed, _rigidbody.linearVelocity.y);
        }

        public void SetDirection(Vector2 direction) => _direction = direction;

        public void AddGroundMove(Vector3 offset)
        {
            _rigidbody.position += (Vector2)offset;
        }
    }

    public interface IMovabele
    {
        void SetDirection(Vector2 direction);
        void AddGroundMove(Vector3 delta);
        void AddCondition(Func<bool> isDead);
    }
}