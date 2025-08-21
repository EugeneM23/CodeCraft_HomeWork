using System;
using Modules;
using SnakeGame;
using Zenject;

namespace Game
{
    public class ScoreUIPresenter : IInitializable, IDisposable
    {
        private readonly GameUI _gameUI;
        private readonly Score _score;

        public ScoreUIPresenter(Score score, GameUI gameUI)
        {
            _score = score;
            _gameUI = gameUI;
        }

        public void Initialize()
        {
            UpdateScore(_score.Current);
            _score.OnStateChanged += UpdateScore;
        }

        public void Dispose() => _score.OnStateChanged -= UpdateScore;

        private void UpdateScore(int currentScore)
        {
            _gameUI.SetScore(currentScore.ToString());
        }
    }
}