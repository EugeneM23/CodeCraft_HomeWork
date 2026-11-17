using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The scenes of the game flow: the main menu and the levels, plus the level layer — prefabs every level gets on
    /// entry (HUD, pause and results screens), so level scenes do not carry them. The App prefab refers to it
    /// (AppInstaller); the editor cold start reads it from there to tell a menu or level scene from a test one.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private SceneReference _mainMenuScene = new();
        [Tooltip("The levels in the order of the game; the first one is where Play in the main menu goes")]
        [SerializeField] private List<LevelConfig> _levels = new();
        [Tooltip("Instantiated into the gameplay scene of every level on entry (HUD, pause, results)")]
        [SerializeField] private List<GameObject> _levelLayer = new();

        public SceneReference MainMenuScene => _mainMenuScene;
        public IReadOnlyList<LevelConfig> Levels => _levels;
        public IReadOnlyList<GameObject> LevelLayer => _levelLayer;

        /// <summary>The first level of the list; null when the list is empty.</summary>
        public LevelConfig FirstLevel => _levels.Count > 0 ? _levels[0] : null;

        /// <summary>The level with the id; null when there is none.</summary>
        public LevelConfig Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] != null && _levels[i].Id == id)
                    return _levels[i];
            }

            return null;
        }

        /// <summary>The first level that has the scene in its set; null when none has.</summary>
        public LevelConfig FindByScene(string scenePath)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] != null && _levels[i].Contains(scenePath))
                    return _levels[i];
            }

            return null;
        }

        public bool IsMenuScene(string scenePath) => _mainMenuScene != null && _mainMenuScene.Is(scenePath);

        /// <summary>Setup from code (tests); the asset sets the same fields in the Inspector.</summary>
        public void Configure(string mainMenuScenePath, params LevelConfig[] levels)
        {
            _mainMenuScene = new SceneReference(mainMenuScenePath);
            _levels = new List<LevelConfig>(levels);
        }

        /// <summary>Setup from code (tests).</summary>
        public void ConfigureLevelLayer(params GameObject[] prefabs) =>
            _levelLayer = new List<GameObject>(prefabs ?? Array.Empty<GameObject>());
    }
}
