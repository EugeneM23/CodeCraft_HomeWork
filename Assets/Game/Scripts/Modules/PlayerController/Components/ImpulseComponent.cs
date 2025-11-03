using UnityEngine;

namespace Modules.PlayerController
{
    internal class ImpulseComponent : IVelocity
    {
        private const float GROUND_FRICTION = 100;
        private const float INPUT_COUNTER_FORCE = 80; // Сила противодействия инпута

        private readonly CharacterController2D _character;

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
            if (_impulse.x == 0)
                return 0;

            float inputDirection = _character.MoveDirection;

            // Если есть инпут в противоположную сторону от начального импульса
            if (inputDirection != 0 && Mathf.Sign(inputDirection) != _initialImpulseSign)
            {
                _wasCountering = true;
                
                // Вычитаем силу противодействия из импульса
                float counterForce = INPUT_COUNTER_FORCE * Time.fixedDeltaTime * -_initialImpulseSign;
                _impulse.x += counterForce;
                
                // Если импульс перешёл через ноль или стал слишком маленьким - обнуляем
                if (Mathf.Sign(_impulse.x) != _initialImpulseSign || Mathf.Abs(_impulse.x) < 0.5f)
                {
                    _impulse.x = 0;
                }
            }
            // Если отпустили кнопки после противодействия - обнуляем импульс
            else if (inputDirection == 0 && _wasCountering)
            {
                _impulse.x = 0;
            }
            // Если персонаж на земле - обычное трение
            else if (_character.IsGrounded) 
            {
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, GROUND_FRICTION * Time.fixedDeltaTime);
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