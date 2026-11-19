using Atomic.Entities;
using Ticking;

namespace Game
{
    /// <summary>The core team's app services. Other teams declare theirs in their own *API files keyed on IAppContext.</summary>
    [EntityExtensionsAPI]
    public static partial class AppContextAPI
    {
        public static readonly ValueKey<IAppContext, IGameLoop> GameLoop = new(nameof(GameLoop));
        public static readonly ValueKey<IAppContext, AppTicker> AppTicker = new(nameof(AppTicker));
        public static readonly ValueKey<IAppContext, GameFlow> GameFlow = new(nameof(GameFlow));
        public static readonly ValueKey<IAppContext, LevelCatalog> LevelCatalog = new(nameof(LevelCatalog));
        public static readonly ValueKey<IAppContext, ILevelLoader> LevelLoader = new(nameof(LevelLoader));
        public static readonly ValueKey<IAppContext, ILoadingScreen> LoadingScreen = new(nameof(LoadingScreen));
        public static readonly ValueKey<IAppContext, SettingsStore> Settings = new(nameof(Settings));
        public static readonly ValueKey<IAppContext, IPlayerProgress> Progress = new(nameof(Progress));
        public static readonly ValueKey<IAppContext, PauseService> PauseService = new(nameof(PauseService));
        public static readonly ValueKey<IAppContext, GameCatalog> GameCatalog = new(nameof(GameCatalog));
        public static readonly ValueKey<IAppContext, IMetaProgress> MetaProgress = new(nameof(MetaProgress));
    }
}
