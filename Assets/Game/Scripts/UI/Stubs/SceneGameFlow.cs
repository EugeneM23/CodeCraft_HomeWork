using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// STUB of the core team's game flow: loads scenes by name at once (no Boot, no loading screen).
    /// Level ids map to scenes through the level catalog the menus register (AppServices.RegisterLevels).
    /// </summary>
    public sealed class SceneGameFlow : IGameFlow
    {
        public const string MENU_SCENE = "MainMenu";

        public event Action<GameState> StateChanged;

        public LevelCatalogConfig Levels { get; set; }

        public GameState State { get; private set; } = GameState.Starting;
        public bool IsBusy => false;
        public string CurrentLevelId { get; private set; }
        public string RequestedMenuScreen { get; private set; }

        public SceneGameFlow()
        {
            // Started straight from a level scene in the editor: the flow still knows where it is.
            if (SceneManager.GetActiveScene().name == MENU_SCENE)
                Enter(GameState.MainMenu);
        }

        public bool StartLevel(string levelId)
        {
            LevelConfig level = Levels != null ? Levels.Find(levelId) : null;
            if (level == null)
            {
                Debug.LogWarning($"SceneGameFlow: no level '{levelId}' in the registered catalog.");
                return false;
            }

            CurrentLevelId = levelId;
            Load(level.sceneName, GameState.Level);
            return true;
        }

        public bool RestartLevel()
        {
            if (State == GameState.MainMenu)
                return false;

            Load(SceneManager.GetActiveScene().name, GameState.Level);
            return true;
        }

        public bool ReturnToMenu(string screen = null)
        {
            RequestedMenuScreen = screen;
            CurrentLevelId = null;
            Load(MENU_SCENE, GameState.MainMenu);
            return true;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Load(string scene, GameState state)
        {
            AppServices.Pause.Resume();
            SceneManager.LoadScene(scene);
            Enter(state);
        }

        private void Enter(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
