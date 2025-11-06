using System;
using UnityEngine;

namespace Modules.PlayerController
{
    internal class ImpulseComponent : IVelocity
    {
        private const float GROUND_FRICTION = 100;
        private const float INPUT_COUNTER_FORCE = 60;

        private readonly CharacterController2D _character;
        private Vector2 _impulse;
        private bool _verticalApplied;
        private float _initialImpulseSign;
        private bool _wasCountering;

        private bool _hasJustEnded;
        private float _endVelocity;

        public bool HasJustEnded => _hasJustEnded;
        public float EndVelocity => _endVelocity;

        public ImpulseComponent(CharacterController2D character)
        {
            _character = character;
            _character.OnCollisionHit += Reset;
        }

        ~ImpulseComponent() => _character.OnCollisionHit -= Reset;

        public void AddImpulse(Vector2 value)
        {
            _impulse = value;
            _verticalApplied = false;
            _initialImpulseSign = Mathf.Sign(value.x);
            _wasCountering = false;
            _hasJustEnded = false;
        }

        private void Reset()
        {
            _impulse = Vector2.zero;
            _verticalApplied = false;
            _initialImpulseSign = 0;
            _wasCountering = false;
            _hasJustEnded = false;
        }

        public void AcknowledgeEnd()
        {
            _hasJustEnded = false;
        }

        public Vector2 GetVelocity()
        {
            float x = UpdateHorizontal();
            float y = UpdateVertical();
            return new Vector2(x, y);
        }

        private float UpdateHorizontal()
        {
            if (Mathf.Approximately(_impulse.x, 0))
                return 0;

            float inputDirection = _character.MoveDirection.x;

            // Противодействие
            if (inputDirection != 0 && Mathf.Sign(inputDirection) != _initialImpulseSign)
            {
                _wasCountering = true;
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, INPUT_COUNTER_FORCE * Time.fixedDeltaTime);

                // Полное гашение
                if (Mathf.Abs(_impulse.x) < 0.1f)
                {
                    _endVelocity = _character.Velocity.x;
                    _hasJustEnded = true;
                    _impulse.x = 0;
                    _initialImpulseSign = 0;
                }
            }
            else if (inputDirection == 0 && _wasCountering)
            {
                _endVelocity = _character.Velocity.x;
                _hasJustEnded = true;
                _impulse.x = 0;
                _initialImpulseSign = 0;
            }
            else if (_character.IsGrounded)
            {
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, GROUND_FRICTION * Time.fixedDeltaTime);

                if (Mathf.Abs(_impulse.x) < 0.1f)
                {
                    _endVelocity = _character.Velocity.x;
                    _hasJustEnded = true;
                    _impulse.x = 0;
                }
            }

            return _impulse.x;
        }

        private float UpdateVertical()
        {
            if (_verticalApplied || _impulse.y == 0)
                return 0;

            _verticalApplied = true;
            return _impulse.y;
        }
    }
}

