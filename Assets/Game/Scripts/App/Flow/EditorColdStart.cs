#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Play in the editor right from the main menu or a level scene of the catalog: before the scene's objects wake,
    /// the App prefab is made just as the Boot scene makes it, and once they are awake the game flow enters the scene
    /// as it is (a level gets its missing scenes and its level layer). Play in Boot, in a test scene, in a sandbox
    /// outside the catalog or with Tools → Game → Cold Start off is left alone. Editor only: a build starts at Boot.
    /// </summary>
    public static class EditorColdStart
    {
        public const string APP_PREFAB_PATH = "Assets/Game/Prefabs/App/App.prefab";
        public const string ENABLED_PREF_KEY = "Game.EditorColdStart.Enabled";

        private const string TEST_SCENE_PREFIX = "InitTestScene";

        private static AppContext s_app;
        private static Scene s_scene;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(ENABLED_PREF_KEY, true);
            set => EditorPrefs.SetBool(ENABLED_PREF_KEY, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void MakeApp()
        {
            // Statics outlive a Play without domain reload
            s_app = null;
            s_scene = default;

            if (!Enabled)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path) || scene.name.StartsWith(TEST_SCENE_PREFIX, System.StringComparison.Ordinal))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<AppContext>(APP_PREFAB_PATH);
            if (prefab == null)
                return;

            AppInstaller installer = prefab.GetComponentInChildren<AppInstaller>(true);
            LevelCatalog catalog = installer != null ? installer.Catalog : null;
            if (catalog == null || (!catalog.IsMenuScene(scene.path) && catalog.FindByScene(scene.path) == null))
                return;

            if (AppContext.TryGet(out AppContext _))
                return;

            s_scene = scene;
            s_app = AppBootstrap.CreateApp(prefab);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnterScene()
        {
            if (s_app == null)
                return;

            if (s_app.TryGetGameFlow(out GameFlow flow))
                flow.EnterOpenScene(s_scene);

            s_app = null;
        }
    }
}
#endif
