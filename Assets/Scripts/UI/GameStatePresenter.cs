using SnakeGame;

namespace Game
{
    public class GameStatePresenter : IGameOverListener, IGameWinListener
    {
        private readonly GameUI _gameUI;

        public GameStatePresenter(GameUI gameUI) => _gameUI = gameUI;

        public void FinishGame() => _gameUI.GameOver(false);

        public void WinGame() => _gameUI.GameOver(true);
    }
}