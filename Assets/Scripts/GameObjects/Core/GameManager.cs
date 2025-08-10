using System;
using System.Collections.Generic;
using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class GameManager : IInitializable, IDisposable
    {
        private readonly List<ILevelStartListener> _gameStartListeners;
        private readonly List<IGameOverListener> _gameFinishListeners;
        private readonly List<IGameWinListener> _gameWinListeners;

        private readonly CoinManager _coinManager;
        private readonly Difficulty _difficulty;

        public GameManager(
            List<ILevelStartListener> gameStartListeners,
            List<IGameOverListener> gameFinishListeners,
            CoinManager coinManager,
            Difficulty difficulty,
            List<IGameWinListener> gameWinListeners
        )
        {
            _gameStartListeners = gameStartListeners;
            _gameFinishListeners = gameFinishListeners;
            _coinManager = coinManager;
            _difficulty = difficulty;
            _gameWinListeners = gameWinListeners;
        }

        public void Initialize()
        {
            _coinManager.OnlevelComplited += StartLevel;

            StartLevel();
        }

        public void Dispose() => _coinManager.OnlevelComplited -= StartLevel;

        private void StartLevel()
        {
            if (_difficulty.Current == _difficulty.Max)
            {
                foreach (var listener in _gameWinListeners)
                    listener.WinGame();

                return;
            }

            foreach (var listener in _gameStartListeners)
                listener.StartLevel();
        }

        public void GameOver()
        {
            foreach (var listener in _gameFinishListeners)
                listener.FinishGame();
        }
    }
}