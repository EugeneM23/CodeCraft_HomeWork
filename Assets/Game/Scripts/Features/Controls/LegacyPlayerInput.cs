using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's controls off the old Input Manager: W/S/A/D (the Horizontal and Vertical axes)
    /// drive, Shift is full power, the left mouse button fires the main gun and the right one the
    /// secondary, the mouse aims and, held, turns the camera; a double middle click resets the
    /// camera, Esc lets the cursor go and a click takes it back. To be replaced by the Input System.
    /// </summary>
    public sealed class LegacyPlayerInput : IPlayerInput
    {
        private const int MAIN_BUTTON = 0;
        private const int SECONDARY_BUTTON = 1;
        private const int MIDDLE_BUTTON = 2;

        public Vector2 Drive => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        public bool Boost => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        public bool FireMain => Input.GetMouseButton(MAIN_BUTTON);

        public bool FireSecondary => Input.GetMouseButton(SECONDARY_BUTTON);

        public Vector2 Pointer => Input.mousePosition;

        public Vector2 Look => new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        public bool ResetLookPressed => Input.GetMouseButtonDown(MIDDLE_BUTTON);

        public bool FreeCursorPressed => Input.GetKeyDown(KeyCode.Escape);

        public bool HoldCursorPressed => Input.GetMouseButtonDown(MAIN_BUTTON) || Input.GetMouseButtonDown(SECONDARY_BUTTON);
    }
}
