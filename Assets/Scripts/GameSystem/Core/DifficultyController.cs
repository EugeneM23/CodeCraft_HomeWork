using System;
using Modules;
using Zenject;

namespace Game
{
    public class DifficultyController : IInitializable, IDisposable
    {
        private readonly Difficulty _difficulty;
        private readonly CoinManager _coinManager;
        private readonly GameCycle _gameCycle;

        public DifficultyController(Difficulty difficulty, CoinManager coinManager, GameCycle gameCycle)
        {
            _difficulty = difficulty;
            _coinManager = coinManager;
            _gameCycle = gameCycle;
        }

        public void Initialize()
        {
            IncreaseDifficulty();
            _coinManager.OnCoinsEmpty += IncreaseDifficulty;
        }

        public void Dispose()
        {
            _coinManager.OnCoinsEmpty -= IncreaseDifficulty;
        }

        private void IncreaseDifficulty()
        {
            if (!_difficulty.Next(out _))
                _gameCycle.WinGame();
        }
    }
}