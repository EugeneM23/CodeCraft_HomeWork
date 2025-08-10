using SnakeGame;

namespace Game
{
    public class GameOverUIHandler : IGameOverListener
    {
        private readonly GameUI _gameUI;

        public GameOverUIHandler(GameUI gameUI)
        {
            _gameUI = gameUI;
        }

        public void FinishGame()
        {
            _gameUI.GameOver(false);
        }
    }
}