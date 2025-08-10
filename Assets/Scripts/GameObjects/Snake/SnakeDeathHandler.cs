using Modules;
using Zenject;

namespace Game
{
    public class SnakeDeathHandler : IInitializable
    {
        private readonly SnakeCollisionComponent _snakeCollisionComponent;
        private readonly Snake _snake;
        private readonly GameManager _gameManager;

        public SnakeDeathHandler(GameManager gameManager, SnakeCollisionComponent snakeCollisionComponent,
            Snake snake)
        {
            _gameManager = gameManager;
            _snakeCollisionComponent = snakeCollisionComponent;
            _snake = snake;
        }

        public void Initialize()
        {
            _snake.OnSelfCollided += _gameManager.GameOver;
            _snakeCollisionComponent.OnDeath += _gameManager.GameOver;
        }
    }
}