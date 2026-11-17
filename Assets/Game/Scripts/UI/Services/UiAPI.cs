using Atomic.Entities;

namespace Game
{
    /// <summary>The menu team's app services, keyed on IAppContext like the core's AppContextAPI.</summary>
    [EntityExtensionsAPI]
    public static partial class UiAPI
    {
        public static readonly ValueKey<IAppContext, IGameInput> GameInput = new(nameof(GameInput));
    }
}
