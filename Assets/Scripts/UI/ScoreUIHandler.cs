using Modules;
using SnakeGame;
using Zenject;

namespace Game
{
    public class ScoreUIHandler : IInitializable
    {
        private readonly GameUI _gameUI;
        private readonly Score _score;
        private readonly SnakeCollisionComponent _snakeCollisionComponent;

        public ScoreUIHandler(Score score, SnakeCollisionComponent snakeCollisionComponent, GameUI gameUI)
        {
            _score = score;
            _snakeCollisionComponent = snakeCollisionComponent;
            _gameUI = gameUI;
        }

        public void Initialize()
        {
            _gameUI.SetScore(_score.Current.ToString());
            _snakeCollisionComponent.CoinPicked += AddScore;
        }

        private void AddScore(Coin coin)
        {
            _score.Add(coin.Score);
            _gameUI.SetScore(_score.Current.ToString());
        }
    }
}