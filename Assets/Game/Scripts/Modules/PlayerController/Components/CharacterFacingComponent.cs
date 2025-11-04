using System;

namespace Modules.PlayerController
{
    public class CharacterFacingComponent : ITickable
    {
        private readonly CharacterController2D _character;

        public int FacingDirection { get; private set; } = 1;

        public event Action<int>? OnFacingChanged;

        public CharacterFacingComponent(CharacterController2D character)
        {
            _character = character;
        }

        public void Tick()
        {
            int newDirection = FacingDirection;

            if (_character.WallDirection != 0 && _character.IsWallSliding && !_character.IsGrounded)
                newDirection = _character.WallDirection;
            else if (_character.MoveDirection.x > 0.01f)
                newDirection = 1;
            else if (_character.MoveDirection.x < -0.01f)
                newDirection = -1;

            if (newDirection != FacingDirection)
            {
                FacingDirection = newDirection;
                OnFacingChanged?.Invoke(FacingDirection);
            }
        }
    }
}