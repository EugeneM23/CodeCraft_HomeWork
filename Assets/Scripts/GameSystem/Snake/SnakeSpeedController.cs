using System;
using Modules;
using Zenject;

namespace Game
{
    public class SnakeSpeedController : IInitializable, IDisposable
    {
        private const int START_LEVEL = 1;
        
        private readonly GameSettings _gameSet;
        private readonly Difficulty _difficulty;
        private readonly Snake _snake;

        private int _currentSpeed;

        public SnakeSpeedController(Difficulty difficulty, Snake snake, GameSettings gameSet)
        {
            _difficulty = difficulty;
            _snake = snake;
            _gameSet = gameSet;
        }

        public void Initialize()
        {
            _currentSpeed = _gameSet.StartSpeed;
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
                _snake.SetSpeed(_gameSet.StartSpeed);
                return;
            }

            _snake.SetSpeed(_currentSpeed += _gameSet.Acceleration);
        }
    }
}