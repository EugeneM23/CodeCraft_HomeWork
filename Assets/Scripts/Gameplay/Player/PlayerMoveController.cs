namespace Gameplay
{
    public class PlayerMoveController
    {
        private readonly PlayerInput _input;
        private readonly Character _player;

        public PlayerMoveController(PlayerInput input, Character player)
        {
            _input = input;
            _player = player;

            Subscription();
        }

        private void Subscription()
        {
            _input.OnMove += _player.Move;
        }
    }
}