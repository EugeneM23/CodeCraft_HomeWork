using Modules;
using Zenject;

namespace Game
{
    public class SnakeSpeedHandler : IInitializable
    {
        private readonly GameSetings _gameSet;
        private readonly Difficulty _difficulty;
        private readonly Snake _snake;

        public SnakeSpeedHandler(Difficulty difficulty, Snake snake, GameSetings gameSet)
        {
            _difficulty = difficulty;
            _snake = snake;
            _gameSet = gameSet;
        }

        public void Initialize()
        {
            _snake.SetSpeed(_gameSet.Acceleration * _difficulty.Current);
        }
    }
}