using Modules;

namespace Game
{
    public class DifficultyController : ILevelStartListener
    {
        private readonly Difficulty _difficulty;

        public DifficultyController(Difficulty difficulty)
        {
            _difficulty = difficulty;
        }

        public void StartLevel()
        {
            _difficulty.Next(out int _);
        }
    }
}