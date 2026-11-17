using UnityEngine;

namespace Game
{
    /// <summary>
    /// The entry point of the game: the only object of the Boot scene, the first scene of the build. Makes the App
    /// prefab and boots the game flow, which covers the screen and goes on into the main menu. The Boot scene loaded
    /// again while the game runs does nothing.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Tooltip("Assets/Game/Prefabs/App/App.prefab")]
        [SerializeField] private AppContext _appPrefab;

        /// <summary>
        /// Makes the app from its prefab: its installers run in Awake inside this call, so the services are ready on
        /// return. Shared with the editor cold start. Null — no prefab.
        /// </summary>
        public static AppContext CreateApp(AppContext prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"{nameof(AppBootstrap)}: no App prefab, the game cannot start");
                return null;
            }

            AppContext app = Instantiate(prefab);
            app.name = prefab.name;
            // Also set on the prefab (AppContext's Dont Destroy On Load); here in case it is not
            DontDestroyOnLoad(app.gameObject);
            return app;
        }

        private void Awake()
        {
            if (AppContext.TryGet(out AppContext _))
            {
                Debug.LogWarning($"{nameof(AppBootstrap)}: the game is running already, the Boot scene does nothing", this);
                return;
            }

            AppContext app = CreateApp(_appPrefab);
            if (app == null)
                return;

            if (app.TryGetGameFlow(out GameFlow flow))
                flow.Boot();
            else
                Debug.LogError($"{nameof(AppBootstrap)}: the App prefab has no game flow (AppInstaller)", this);
        }
    }
}
