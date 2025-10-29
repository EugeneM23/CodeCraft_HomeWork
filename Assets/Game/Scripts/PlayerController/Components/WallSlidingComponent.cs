using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent : IVelocity
    {
        private readonly PlayerController _player;
        private Vector2 _currentVelocity;

        public WallSlidingComponent(PlayerController player)
        {
            _player = player;
        }

        public Vector2 GetVelocity()
        {
            if (!_player.IsOnWall)
                return Vector2.zero;

            if (_player.Velocity.y > 0.5f)
                return Vector2.zero;

            int wallDir = _player.WallDirection;
            float inputX = _player.MoveDirection.x;

            // Проверяем, жмёт ли игрок в сторону стены
            if (Mathf.Abs(inputX) < 0.1f)
                return Vector2.zero; // Не жмём — не скользим

            float directionToWall = inputX * wallDir;

            // Если жмём ОТ стены — не скользим
            if (directionToWall < 0)
                return Vector2.zero;

            // Жмём В стену — скользим
            float slideSpeed = _player.Stats.WallSlideSpeed;
            return new Vector2(0, -slideSpeed);
        }
    }
}