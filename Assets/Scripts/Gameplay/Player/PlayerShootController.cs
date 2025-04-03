namespace Gameplay
{
    public class PlayerShootController
    {
        private readonly PlayerInput _input;
        private readonly Character _player;

        public PlayerShootController(PlayerInput input, Character player)
        {
            _input = input;
            _player = player;

            Subscription();
        }

        private void Subscription() => _input.OnShoot += _player.Shoot;
    }
}