using Gameplay;
using UnityEngine;

namespace Modules.PlayerController
{
    internal class WallSlidingComponent : ITickable, IVelocity
    {
        private readonly CharacterController2D _character;

        public bool IsWallSliding { get; private set; }

        public WallSlidingComponent(CharacterController2D character)
        {
            _character = character;
        }

        public void Tick()
        {
            if (_character.IsOnWall && Mathf.Abs(_character.MoveDirection.x) > 0.1f)
            {
                IsWallSliding = _character.MoveDirection.x * _character.WallDirection > 0;
            }
            else
            {
                IsWallSliding = false;
            }
        }

        public Vector2 GetVelocity()
        {
            if (!IsWallSliding || _character.Velocity.y > 0.5f)
                return Vector2.zero;

            return new Vector2(0, -_character.Stats.WallSlideSpeed);
        }
    }
}