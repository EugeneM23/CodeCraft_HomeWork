using UnityEngine;

namespace Modules.PlayerController
{
    internal class ImpulseComponent : IVelocity
    {
        private const float GROUND_FRICTION = 100;
        private const float INPUT_COUNTER_FORCE = 60; // Сила противодействия инпута

        private readonly CharacterController2D _character;
        public event System.Action<float> OnImpulseEnd;
        private Vector2 _impulse;
        private bool _verticalApplied;
        private float _initialImpulseSign; // Запоминаем начальное направление импульса
        private bool _wasCountering; // Было ли противодействие

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
        }

        private void Reset()
        {
            _impulse = Vector2.zero;
            _verticalApplied = false;
            _initialImpulseSign = 0;
            _wasCountering = false;
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

            float inputDirection = _character.MoveDirection;

            // Противодействие
            if (inputDirection != 0 && Mathf.Sign(inputDirection) != _initialImpulseSign)
            {
                _wasCountering = true;
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, INPUT_COUNTER_FORCE * Time.fixedDeltaTime);

                // Полное гашение
                if (Mathf.Abs(_impulse.x) < 0.1f)
                {
                    // Передаём остаточную скорость в MoveComponent, чтобы тот продолжил плавно
                    OnImpulseEnd?.Invoke(_character.Velocity.x);
                    _impulse.x = 0;
                    _initialImpulseSign = 0;
                }
            }
            else if (inputDirection == 0 && _wasCountering)
            {
                OnImpulseEnd?.Invoke(_character.Velocity.x);
                _impulse.x = 0;
                _initialImpulseSign = 0;
            }
            else if (_character.IsGrounded)
            {
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, GROUND_FRICTION * Time.fixedDeltaTime);

                if (Mathf.Abs(_impulse.x) < 0.1f)
                {
                    OnImpulseEnd?.Invoke(_character.Velocity.x);
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