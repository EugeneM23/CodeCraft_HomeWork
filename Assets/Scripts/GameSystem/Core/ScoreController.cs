using System;
using Modules;
using Zenject;

namespace Game
{
    public class ScoreController : IInitializable, IDisposable
    {
        private readonly CoinManager _coinManager;
        private readonly Score _score;

        public ScoreController(Score score, CoinManager coinManager)
        {
            _score = score;
            _coinManager = coinManager;
        }

        public void Initialize() => _coinManager.OnCoinPicked += UpdateScore;

        public void Dispose() => _coinManager.OnCoinPicked -= UpdateScore;

        private void UpdateScore(Coin coin)
        {
            _score.Add(coin.Score);
        }
    }
}