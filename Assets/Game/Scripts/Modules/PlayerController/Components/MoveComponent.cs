using UnityEngine;

namespace Modules.PlayerController
{
    internal class MoveComponent : IVelocity, IMoveComponent
    {
        private readonly PlayerController _player;
        private float _horizontalSpeed;
        private Vector2 _targetDirection;

        public MoveComponent(PlayerController player) => _player = player;

        public void Move(Vector2 direction)
        {
            _targetDirection = direction;
        }

        public Vector2 GetVelocity()
        {
            if (_player.IsOnWall && _player.WallDirection == _player.MoveDirection.x)
                return Vector2.zero;
        
            if (Mathf.Abs(_player.Velocity.x) >= _player.Stats.MaxSpeed + 1)
                return new Vector2(_horizontalSpeed, 0);

            float targetSpeed = _targetDirection.x * _player.Stats.MaxSpeed;

            float acceleration = _player.IsGrounded
                ? _player.Stats.Acceleration
                : _player.Stats.AirAcceleration;

            _horizontalSpeed = Mathf.MoveTowards(
                _horizontalSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );

            return new Vector2(_horizontalSpeed, 0);
        }
    }
}