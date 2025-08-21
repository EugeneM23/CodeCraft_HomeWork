using System;
using Modules;
using SnakeGame;
using Zenject;

namespace Game
{
    public class DifficultyUIPresenter : IInitializable, IDisposable
    {
        private readonly GameUI _gameUI;
        private readonly Difficulty _difficulty;

        public DifficultyUIPresenter(GameUI gameUI, Difficulty difficulty)
        {
            _gameUI = gameUI;
            _difficulty = difficulty;
        }

        public void Initialize()
        {
            UpdateDifficulty();
            _difficulty.OnStateChanged += UpdateDifficulty;
        }

        public void Dispose()
        {
            _difficulty.OnStateChanged -= UpdateDifficulty;
        }

        private void UpdateDifficulty()
        {
            _gameUI.SetDifficulty(_difficulty.Current, _difficulty.Max);
        }
    }
}