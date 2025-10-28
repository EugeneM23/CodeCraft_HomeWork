using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponent : IVelocity
    {
        private readonly PlayerController _player;

        private float _horizontalSpeed;
        private float _lastInputX;
        private Vector2 _currentVelocity;

        public MoveComponent(PlayerController player)
        {
            _player = player;
        }

        public void Move(Vector2 directrion)
        {
            float targetSpeed = directrion.x * _player.Stats.MaxSpeed;

            _horizontalSpeed = Mathf.MoveTowards(
                _horizontalSpeed,
                targetSpeed,
                _player.Stats.Acceleration * Time.fixedDeltaTime
            );

            _currentVelocity = new Vector2(_horizontalSpeed, 0);
        }

        public Vector2 GetVelocity() => _currentVelocity;
    }
}