using SnakeGame;

namespace Game
{
    public class GameWinUIHandler : IGameWinListener
    {
        private readonly GameUI _gameUI;

        public GameWinUIHandler(GameUI gameUI)
        {
            _gameUI = gameUI;
        }

        public void WinGame()
        {
            _gameUI.GameOver(true);
        }
    }
}