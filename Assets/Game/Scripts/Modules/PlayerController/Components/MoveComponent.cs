using UnityEngine;

namespace Modules.PlayerController
{
    internal class MoveComponent : IVelocity, IMoveComponent
    {
        private readonly CharacterController2D _character;
        private readonly ImpulseComponent _impulseComponent;
        private float _horizontalSpeed;
        private Vector2 _targetDirection;

        public MoveComponent(CharacterController2D character, ImpulseComponent impulseComponent)
        {
            _character = character;
            _impulseComponent = impulseComponent;
        }


        public Vector2 GetVelocity()
        {
            _targetDirection = _character.MoveDirection;
            if (!_character.CanMove) return Vector2.zero;

            if (_impulseComponent.HasJustEnded)
            {
                _horizontalSpeed = _impulseComponent.EndVelocity;
                _impulseComponent.AcknowledgeEnd();
            }

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