using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The shell-type switch key (Q). Read straight off the keyboard for now: it is not in GameControls, so it cannot
    /// be rebound in the settings yet (1 and 2 are the skill choice keys).
    /// </summary>
    public static class AmmoInput
    {
        public const string KEY_NAME = "Q";

        public static bool SwitchPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.qKey.wasPressedThisFrame;
        }
    }
}
