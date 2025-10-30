using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Components
{
    internal class ImpulseComponent : IVelocity
    {
        private readonly PlayerController _player;
        private Vector2 _impulse;
        private bool _verticalApplied;

        public ImpulseComponent(PlayerController player)
        {
            _player = player;
            _player.OnHit += Reset;
        }

        ~ImpulseComponent() => _player.OnHit -= Reset;

        public void AddImpulse(Vector2 value)
        {
            _impulse = value;
            _verticalApplied = false;
        }

        private void Reset()
        {
            _impulse = Vector2.zero;
            _verticalApplied = false;
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

            float input = _player.MoveDirection.x;

            if (input != 0)
            {
                // Игрок двигает стик
                bool sameDirection = Mathf.Sign(input) == Mathf.Sign(_impulse.x);

                if (sameDirection)
                {
                    // В ту же сторону - можем ускориться
                    float inputSpeed = input * _player.Stats.MaxSpeed;
                    if (Mathf.Abs(inputSpeed) > Mathf.Abs(_impulse.x))
                        _impulse.x = inputSpeed;
                }
                else
                {
                    // В противоположную - тормозим
                    _impulse.x = Mathf.MoveTowards(_impulse.x, 0, 100 * Time.fixedDeltaTime);
                }
            }
            else
            {
                // Нет инпута - затухание
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, 50 * Time.fixedDeltaTime);
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