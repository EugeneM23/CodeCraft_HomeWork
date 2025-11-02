using Gameplay;
using Modules.PlayerController;

namespace Game.Scripts
{
    public class SpriteFlipComponent : ITickable
    {
        private readonly CharacterController2D _character;

        public SpriteFlipComponent(CharacterController2D character) => _character = character;

        public void Tick()
        {
            if (_character.WallDirection != 0 && _character.IsWallSliding && !_character.IsGrounded)
            {
                if (_character.WallDirection < 0) _character.SpriteRenderer.flipX = true;
                if (_character.WallDirection > 0) _character.SpriteRenderer.flipX = false;
                return;
            }

            if (_character.Velocity.x < 0) _character.SpriteRenderer.flipX = true;
            if (_character.Velocity.x > 0) _character.SpriteRenderer.flipX = false;
        }
    }
}