using Ticking;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The game loop for game MonoBehaviours outside Atomic. With the app running a component joins the loop
    /// (<see cref="TryAdd"/> true) and ticks from it; without the app (tests, demo scenes) it ticks from its own Unity
    /// method. <see cref="Time"/> is game time: the loop's, standing still on pause, or UnityEngine.Time.time without
    /// the app. The loop is bound by <see cref="GameLoopInstaller"/> while the AppContext lives — no scene search.
    /// <code>
    /// private void OnEnable() => _looped = GameTicks.TryAdd(this);
    /// private void OnDisable() { if (_looped) GameTicks.Remove(this); _looped = false; }
    /// private void Update() { if (!_looped) OnUpdate(Time.deltaTime); }
    /// </code>
    /// </summary>
    public static class GameTicks
    {
        private static IGameLoop s_loop;
        private static Object s_owner;

        /// <summary>The app's loop is up.</summary>
        public static bool HasLoop => s_loop != null && s_owner != null;

        /// <summary>Game seconds: the loop's time with the app, otherwise UnityEngine.Time.time.</summary>
        public static float Time => HasLoop ? s_loop.Time : UnityEngine.Time.time;

        /// <summary>The app's loop, or null without the app.</summary>
        public static IGameLoop Loop => HasLoop ? s_loop : null;

        /// <summary>Joins the app's loop if there is one. False — no app: tick from your own Unity method.</summary>
        public static bool TryAdd(object listener)
        {
            if (listener == null || !HasLoop)
                return false;

            s_loop.Add(listener);
            return true;
        }

        public static void Remove(object listener)
        {
            if (listener != null && HasLoop)
                s_loop.Remove(listener);
        }

        /// <summary>The app's loop, alive while the owner (the AppContext) is.</summary>
        public static void Bind(IGameLoop loop, Object owner)
        {
            s_loop = loop;
            s_owner = owner;
        }

        public static void Unbind(IGameLoop loop)
        {
            if (!ReferenceEquals(s_loop, loop))
                return;

            s_loop = null;
            s_owner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_loop = null;
            s_owner = null;
        }
    }
}
