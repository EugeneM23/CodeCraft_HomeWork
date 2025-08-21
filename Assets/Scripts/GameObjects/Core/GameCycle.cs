using System.Collections.Generic;
using Zenject;

namespace Game
{
    public class GameCycle : IInitializable
    {
        private readonly List<IGameStartListener> _gameStartListeners;
        private readonly List<IGameOverListener> _gameFinishListeners;
        private readonly List<IGameWinListener> _gameWinListeners;

        public GameCycle(
            List<IGameStartListener> gameStartListeners,
            List<IGameOverListener> gameFinishListeners,
            List<IGameWinListener> gameWinListeners
        )
        {
            _gameStartListeners = gameStartListeners;
            _gameFinishListeners = gameFinishListeners;
            _gameWinListeners = gameWinListeners;
        }

        public void Initialize() => GameStart();

        private void GameStart()
        {
            foreach (var listener in _gameStartListeners)
                listener.StartGame();
        }

        public void GameWin()
        {
            foreach (var listener in _gameWinListeners)
                listener.WinGame();
        }

        public void GameOver()
        {
            foreach (var listener in _gameFinishListeners)
                listener.FinishGame();
        }
    }
}