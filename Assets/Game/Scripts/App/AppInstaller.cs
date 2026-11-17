using Atomic.Elements;
using Atomic.Entities;
using Ticking;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Installs the app's flow services: the ticker, the level catalog and loader, the loading screen, the pause
    /// service and the game flow (ticked by the ticker on real time). On the App prefab after GameLoopInstaller: the
    /// game loop is taken from the context when it is there (without it there is no pause service and no resume after
    /// transitions). Play Mode only — installers also run in Edit Mode.
    /// </summary>
    public sealed class AppInstaller : MonoEntityInstaller<AppContext>
    {
        [Tooltip("The menu scene, the levels and the level layer")]
        [SerializeField] private LevelCatalog _catalog;
        [Tooltip("The core's loading screen on the App prefab; may be empty (transitions run uncovered)")]
        [SerializeField] private LoadingScreen _loadingScreen;
        [Tooltip("The AppTicker on the App prefab")]
        [SerializeField] private AppTicker _ticker;

        private GameFlow _flow;
        private PauseService _pause;

        /// <summary>For the editor cold start: which scenes the app takes over.</summary>
        public LevelCatalog Catalog => _catalog;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode())
                return;

            if (_catalog == null || _ticker == null)
            {
                Debug.LogError($"{nameof(AppInstaller)}: the level catalog or the ticker is not set, the game flow is off", this);
                return;
            }

            // A missing screen is Unity-null: give the flow a real null
            ILoadingScreen screen = _loadingScreen != null ? _loadingScreen : null;
            IGameLoop loop = context.TryGetGameLoop(out IGameLoop found) ? found : null;
            var loader = new LevelLoader();

            context.AddAppTicker(_ticker);
            context.AddLevelCatalog(_catalog);
            context.AddLevelLoader(loader);
            if (screen != null)
                context.AddLoadingScreen(screen);

            if (loop != null)
            {
                _pause = new PauseService(loop);
                context.AddPauseService(_pause);
            }

            _flow = new GameFlow(_catalog, loader, screen, loop);
            context.AddGameFlow(_flow);
            _ticker.Add(_flow);
        }

        public override void Uninstall(AppContext context)
        {
            if (_flow == null)
                return;

            if (_ticker != null)
                _ticker.Remove(_flow);

            context.DelGameFlow();
            context.DelPauseService();
            context.DelLoadingScreen();
            context.DelLevelLoader();
            context.DelLevelCatalog();
            context.DelAppTicker();

            _pause?.Dispose();
            _pause = null;
            _flow = null;
        }
    }
}
