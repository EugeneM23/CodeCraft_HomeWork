using UnityEngine;

namespace Modules.PlayerController
{
    internal class MoveComponent : IVelocity, IMoveComponent
    {
        private readonly CharacterController2D _character;
        private float _horizontalSpeed;
        private Vector2 _targetDirection;

        public MoveComponent(CharacterController2D character) => _character = character;

        public void Move(Vector2 direction)
        {
            _targetDirection = direction;
        }

        public bool CanMove()
        {
            foreach (var item in _character.MoveCondition)
                if (item.Invoke())
                    return false;

            return true;
        }

        public void InheritVelocity(float horizontal)
        {
            _horizontalSpeed = horizontal;
        }

        public Vector2 GetVelocity()
        {
            if (!CanMove()) return Vector2.zero;

            if (_character.IsOnWall && _character.WallDirection == _character.Velocity.x)
                return Vector2.zero;

            if (Mathf.Abs(_character.Velocity.x) >= _character.Stats.MaxSpeed + 1)
                return new Vector2(_horizontalSpeed, 0);

            float targetSpeed = _targetDirection.x * _character.Stats.MaxSpeed;

            float acceleration = _character.IsGrounded
                ? _character.Stats.Acceleration
                : _character.Stats.AirAcceleration;

            _horizontalSpeed = Mathf.MoveTowards(
                _horizontalSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );

            return new Vector2(_horizontalSpeed, 0);
        }
    }
}