using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The player's controls off the Input System: the Tank map of the game's GameControls (keys rebound in
    /// the settings). While a menu covers the battle the map is off and everything reads as released. Look
    /// is the mouse delta scaled like the old "Mouse X/Y" axes, times the player's sensitivity.
    /// </summary>
    public sealed class InputSystemPlayerInput : IPlayerInput
    {
        // The old Input Manager's "Mouse X/Y" sensitivity: keeps the camera's turn speed meaning what it meant.
        private const float LEGACY_AXIS_SCALE = 0.1f;

        private static GameControls.TankActions Tank => AppServices.Input.Controls.Tank;

        public Vector2 Drive => Tank.Drive.ReadValue<Vector2>();
        public bool Boost => Tank.Boost.IsPressed();
        public bool FireMain => Tank.FireMain.IsPressed();
        public bool FireSecondary => Tank.FireSecondary.IsPressed();
        public Vector2 Pointer => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public Vector2 Look => Tank.Look.ReadValue<Vector2>() * (LEGACY_AXIS_SCALE * AppServices.Input.LookSensitivity);
        public bool ResetLookPressed => Tank.ResetCamera.WasPressedThisFrame();
        public bool FreeCursorPressed => Tank.Pause.WasPressedThisFrame();
        public bool HoldCursorPressed => Tank.FireMain.WasPressedThisFrame() || Tank.FireSecondary.WasPressedThisFrame();
    }
}
