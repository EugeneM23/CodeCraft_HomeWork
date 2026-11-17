using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// One level of the game: the set of scenes it is made of, which of them is active (lighting, new objects) and
    /// which holds the gameplay (the level layer, the HUD). The first scene is loaded Single and replaces whatever was
    /// open, the others come additively.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Game/Level")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Tooltip("Stable id of the level: progress and saves refer to it, do not rename once shipped")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [TextArea(2, 6)]
        [SerializeField] private string _description;
        [Tooltip("The scenes of the level; the first one is loaded Single, the rest additively")]
        [SerializeField] private List<SceneReference> _scenes = new();
        [Tooltip("Index in Scenes of the scene made active after loading")]
        [SerializeField] private int _activeSceneIndex;
        [Tooltip("Index in Scenes of the scene the level layer (HUD, pause, results) goes into; -1 — the active scene")]
        [SerializeField] private int _gameplaySceneIndex = -1;
        [Tooltip("The level that must be completed to unlock this one; none — open from the start")]
        [SerializeField] private LevelConfig _requiredLevel;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public string Description => _description;
        public IReadOnlyList<SceneReference> Scenes => _scenes;
        public int ActiveSceneIndex => _activeSceneIndex;
        public LevelConfig RequiredLevel => _requiredLevel;

        /// <summary>The scene of the gameplay objects and the HUD; the active scene unless set.</summary>
        public int GameplaySceneIndex => _gameplaySceneIndex >= 0 ? _gameplaySceneIndex : _activeSceneIndex;

        /// <summary>Path of the active scene; empty when the index is out of the list.</summary>
        public string ActiveScenePath => PathAt(_activeSceneIndex);

        /// <summary>Path of the gameplay scene; empty when the index is out of the list.</summary>
        public string GameplayScenePath => PathAt(GameplaySceneIndex);

        public bool Contains(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return false;

            for (int i = 0; i < _scenes.Count; i++)
            {
                if (_scenes[i] != null && _scenes[i].Is(scenePath))
                    return true;
            }

            return false;
        }

        /// <summary>Setup from code (tests); the asset sets the same fields in the Inspector.</summary>
        public void Configure(string id, string displayName, int activeSceneIndex, params string[] scenePaths)
        {
            _id = id;
            _displayName = displayName;
            _activeSceneIndex = activeSceneIndex;
            _gameplaySceneIndex = -1;
            _scenes = new List<SceneReference>(scenePaths.Length);
            foreach (string path in scenePaths)
                _scenes.Add(new SceneReference(path));
        }

        /// <summary>Setup from code (tests).</summary>
        public void Configure(int gameplaySceneIndex, LevelConfig requiredLevel)
        {
            _gameplaySceneIndex = gameplaySceneIndex;
            _requiredLevel = requiredLevel;
        }

        private string PathAt(int index) =>
            index >= 0 && index < _scenes.Count && _scenes[index] != null ? _scenes[index].Path : string.Empty;
    }
}
