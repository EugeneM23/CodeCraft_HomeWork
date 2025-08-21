using System;
using Modules;
using Zenject;

namespace Game
{
    public class ScoreController : IInitializable, IDisposable
    {
        private readonly SnakeCollisionComponent _snakeCollisionComponent;
        private readonly Score _score;

        public ScoreController(SnakeCollisionComponent snakeCollisionComponent, Score score)
        {
            _snakeCollisionComponent = snakeCollisionComponent;
            _score = score;
        }

        public void Initialize() => _snakeCollisionComponent.CoinPicked += UpdateScore;

        public void Dispose() => _snakeCollisionComponent.CoinPicked -= UpdateScore;

        private void UpdateScore(Coin coin)
        {
            _score.Add(coin.Score);
        }
    }
}