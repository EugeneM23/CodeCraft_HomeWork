using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// STUB of the core team's game flow: loads scenes by name, nothing in between (no Boot, no
    /// loading screen). Resumes a paused game before leaving the level.
    /// </summary>
    public sealed class SceneGameFlow : IGameFlow
    {
        public const string MENU_SCENE = "MainMenu";

        public LevelConfig CurrentLevel { get; private set; }
        public MenuEntry PendingMenuEntry { get; private set; }

        public void StartLevel(LevelConfig level)
        {
            CurrentLevel = level;
            Load(level.sceneName);
        }

        public void RestartLevel()
        {
            if (CurrentLevel != null)
                Load(CurrentLevel.sceneName);
            else
                Load(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMenu(MenuEntry entry)
        {
            PendingMenuEntry = entry;
            Load(MENU_SCENE);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Load(string scene)
        {
            AppServices.Pause.Resume();
            SceneManager.LoadScene(scene);
        }
    }
}
