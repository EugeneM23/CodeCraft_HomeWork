using Modules;
using UnityEngine;

namespace Game
{
    public class SnakeDisposeController : IGameWinListener, IGameOverListener
    {
        private readonly Snake _snake;

        public SnakeDisposeController(Snake snake) => _snake = snake;

        public void WinGame() => Object.Destroy(_snake.gameObject);

        public void GameOver() => Object.Destroy(_snake.gameObject);
    }
}