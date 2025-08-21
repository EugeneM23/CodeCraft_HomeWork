using System;
using Modules;
using Zenject;

namespace Game
{
    public class SnakeSpeedController : IInitializable, IDisposable
    {
        private const int START_LEVEL = 1;

        private readonly GameSettings _gameSettings;
        private readonly Difficulty _difficulty;
        private readonly Snake _snake;

        private int _currentSpeed;

        public SnakeSpeedController(Difficulty difficulty, Snake snake, GameSettings gameSettings)
        {
            _difficulty = difficulty;
            _snake = snake;
            _gameSettings = gameSettings;
        }

        public void Initialize()
        {
            _currentSpeed = _gameSettings.StartSpeed;
            _difficulty.OnStateChanged += IncreaseSpeed;
        }

        public void Dispose()
        {
            _difficulty.OnStateChanged -= IncreaseSpeed;
        }

        private void IncreaseSpeed()
        {
            if (_difficulty.Current == START_LEVEL)
            {
                _snake.SetSpeed(_gameSettings.StartSpeed);
                return;
            }

            _snake.SetSpeed(_currentSpeed += _gameSettings.Acceleration);
        }
    }
}