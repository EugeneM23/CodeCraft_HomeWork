using Modules;
using Zenject;

namespace Game
{
    public class SnakeDeathHandler : IInitializable
    {
        private readonly SnakeCollisionComponent _snakeCollisionComponent;
        private readonly Snake _snake;
        private readonly GameCycle _gameCycle;

        public SnakeDeathHandler(GameCycle gameCycle, SnakeCollisionComponent snakeCollisionComponent,
            Snake snake)
        {
            _gameCycle = gameCycle;
            _snakeCollisionComponent = snakeCollisionComponent;
            _snake = snake;
        }

        public void Initialize()
        {
            _snake.OnSelfCollided += _gameCycle.GameOver;
            _snakeCollisionComponent.OnDeath += _gameCycle.GameOver;
        }
    }
}