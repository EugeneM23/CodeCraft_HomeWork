using SnakeGame;

namespace Game
{
    public class GameStateUIPresenter : IGameOverListener, IGameWinListener
    {
        private readonly GameUI _gameUI;

        public GameStateUIPresenter(GameUI gameUI) => _gameUI = gameUI;

        public void GameOver() => _gameUI.GameOver(false);

        public void WinGame() => _gameUI.GameOver(true);
    }
}