using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>Debug: F10 leaves the level for the main menu. Lives in the level scene; without the app does nothing.</summary>
    public sealed class LevelExitHotkey : MonoBehaviour
    {
        private void Update() // perf-ok: ticks during pause (F10 works on the paused game)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f10Key.wasPressedThisFrame)
                return;

            if (AppContext.TryGet(out AppContext app) && app.TryGetGameFlow(out GameFlow flow))
                flow.ReturnToMenu();
        }
    }
}
