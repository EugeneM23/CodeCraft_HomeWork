using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Esc pauses and resumes the game through the PauseService — a stand-in until the menu team's pause screen.
    /// Lives in the level scene. Enabled = false lets a menu take Esc over. Without the app (a sandbox Play) does
    /// nothing.
    /// </summary>
    public sealed class PauseHotkey : MonoBehaviour
    {
        /// <summary>Off — Esc is someone else's (a menu screen is open).</summary>
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Enabled = true;

        private void Update() // perf-ok: ticks during pause (Esc must resume the game)
        {
            if (!Enabled)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (!AppContext.TryGet(out AppContext app) || !app.TryGetPauseService(out PauseService pause))
                return;

            if (pause.IsHeld)
                pause.Resume();
            else
                pause.Pause();
        }

        // The level unloads while paused: the game must not come into the next scene standing
        private void OnDisable()
        {
            if (AppContext.TryGet(out AppContext app) && app.TryGetPauseService(out PauseService pause))
                pause.Resume();
        }
    }
}
