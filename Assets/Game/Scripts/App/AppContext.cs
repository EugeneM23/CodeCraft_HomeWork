using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Lives the whole game (set Dont Destroy On Load on the App prefab). Made by the Boot scene or, in the editor, by
    /// the cold start. Services are installed by MonoEntityInstaller&lt;AppContext&gt; components on the same prefab.
    /// </summary>
    public sealed class AppContext : MonoEntitySingleton<AppContext>, IAppContext
    {
        /// <summary>
        /// The app if it is up; never throws. Searches the scenes only while there is no app — call it on events
        /// (a key press, OnEnable), not every frame.
        /// </summary>
        public static bool TryGet(out AppContext app) => TryGetInstance(out app);
    }
}
