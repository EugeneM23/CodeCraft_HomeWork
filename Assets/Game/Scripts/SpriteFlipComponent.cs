using Gameplay;
using Modules.PlayerController;

namespace Game.Scripts
{
    public class SpriteFlipComponent : ITickable
    {
        private readonly PlayerController _player;

        public SpriteFlipComponent(PlayerController player) => _player = player;

        public void Tick()
        {
            if (_player.WallDirection != 0 && _player.IsWallSliding && !_player.IsGrounded)
            {
                if (_player.WallDirection < 0) _player.SpriteRenderer.flipX = true;
                if (_player.WallDirection > 0) _player.SpriteRenderer.flipX = false;
                return;
            }

            if (_player.Velocity.x < 0) _player.SpriteRenderer.flipX = true;
            if (_player.Velocity.x > 0) _player.SpriteRenderer.flipX = false;
        }
    }
}