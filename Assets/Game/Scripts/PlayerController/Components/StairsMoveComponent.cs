using UnityEngine;

namespace PlayerController
{
    internal class StairsMoveComponent : IVelocity
    {
        private readonly PlayerController _player;

        private float _currentHorizontalSpeed;
        private float _moveY;

        private Vector2 _currentVelocity;

        public StairsMoveComponent(PlayerController player) => _player = player;

        public void Move(Vector2 directrion)
        {
            if (!_player.IsOnStairs)
                _currentVelocity = Vector2.zero;

            if (directrion.y != 0)
                _moveY = directrion.y * 5;
            else
                _currentVelocity = Vector2.zero;

            _currentVelocity = new Vector2(0, _moveY);
        }

        public Vector2 GetVelocity() => _currentVelocity;
    }
}