using Modules;
using Zenject;

namespace Game
{
    public class SnakeExpandHandler : IInitializable
    {
        private readonly Snake _snake;
        private readonly SnakeCollisionComponent _snakeCollisionComponent;

        public SnakeExpandHandler(SnakeCollisionComponent snakeCollisionComponent, Snake snake)
        {
            _snakeCollisionComponent = snakeCollisionComponent;
            _snake = snake;
        }

        public void Initialize()
        {
            _snakeCollisionComponent.CoinPicked += ExpandSnake;
        }

        private void ExpandSnake(Coin coin) => _snake.Expand(coin.Bones);
    }
}