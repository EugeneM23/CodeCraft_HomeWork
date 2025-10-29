using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class MoveComponent : IVelocity
    {
        private readonly PlayerController _player;

        private float _horizontalSpeed;
        private float _lastInputX;
        private Vector2 _currentVelocity;

        public MoveComponent(PlayerController player) => _player = player;

        public void Move(Vector2 direction)
        {
            float targetSpeed = direction.x * _player.Stats.MaxSpeed;

            // ИСПРАВЛЕНИЕ: Выбираем ускорение в зависимости от того, на земле ли персонаж
            float acceleration = _player.IsGrounded 
                ? _player.Stats.Acceleration 
                : _player.Stats.AirAcceleration; // Новый параметр для воздуха

            _horizontalSpeed = Mathf.MoveTowards(
                _horizontalSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );

            _currentVelocity = new Vector2(_horizontalSpeed, 0);
        }

        public Vector2 GetVelocity() => _currentVelocity;
    }
}