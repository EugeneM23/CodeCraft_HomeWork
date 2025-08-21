using System;
using Modules;
using Zenject;

namespace Game
{
    public class CoinSpawnController : IInitializable, IDisposable
    {
        private readonly CoinManager _coinManager;
        private readonly Difficulty _difficulty;

        public CoinSpawnController(CoinManager coinManager, Difficulty difficulty)
        {
            _coinManager = coinManager;
            _difficulty = difficulty;
        }

        public void Initialize()
        {
            _difficulty.OnStateChanged += OnDifficultyChange;
            _coinManager.SpawnCoin(_difficulty.Current);
        }

        public void Dispose()
        {
            _difficulty.OnStateChanged -= OnDifficultyChange;
        }

        public void OnDifficultyChange()
        {
            _coinManager.SpawnCoin(_difficulty.Current);
        }
    }
}