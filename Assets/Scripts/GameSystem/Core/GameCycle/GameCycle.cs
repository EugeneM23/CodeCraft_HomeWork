using System.Collections.Generic;
using Zenject;

namespace Game
{
    public class GameCycle : IInitializable
    {
        private readonly List<IGameStartListener> _gameStartListeners;
        private readonly List<IGameOverListener> _gameOverListeners;
        private readonly List<IGameWinListener> _gameWinListeners;

        public GameCycle(
            List<IGameStartListener> gameStartListeners,
            List<IGameOverListener> gameOverListeners,
            List<IGameWinListener> gameWinListeners
        )
        {
            _gameStartListeners = gameStartListeners;
            _gameOverListeners = gameOverListeners;
            _gameWinListeners = gameWinListeners;
        }

        public void Initialize() => StartGame();

        private void StartGame()
        {
            foreach (var listener in _gameStartListeners)
                listener.StartGame();
        }

        public void WinGame()
        {
            foreach (var listener in _gameWinListeners)
                listener.WinGame();
        }

        public void GameOver()
        {
            foreach (var listener in _gameOverListeners)
                listener.GameOver();
        }
    }
}