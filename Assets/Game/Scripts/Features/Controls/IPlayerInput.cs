using UnityEngine;

namespace Game
{
    /// <summary>
    /// What the player asks for this frame: the one place the controls are read. The player
    /// context holds it (PlayerContextAPI.PlayerInput); the controllers, the camera look and the
    /// sandbox's mouse look read it and never the devices themselves, so a different source of input
    /// (the Input System, a replay, a test) replaces this one object and nothing else.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>The drive stick: x turns the hull (-1 left .. 1 right), y is the throttle (-1 reverse .. 1 forward).</summary>
        Vector2 Drive { get; }

        /// <summary>Full engine power, held.</summary>
        bool Boost { get; }

        /// <summary>The main gun, held.</summary>
        bool FireMain { get; }

        /// <summary>The secondary gun, held.</summary>
        bool FireSecondary { get; }

        /// <summary>Where the pointer is on the screen, in pixels. Not read while the cursor is locked.</summary>
        Vector2 Pointer { get; }

        /// <summary>How far the camera is turned this frame while the cursor is held (mouse movement).</summary>
        Vector2 Look { get; }

        /// <summary>Pressed this frame: the camera's turn goes back to the start (twice in a row).</summary>
        bool ResetLookPressed { get; }

        /// <summary>Pressed this frame: the cursor is let go.</summary>
        bool FreeCursorPressed { get; }

        /// <summary>Pressed this frame: a let-go cursor is taken back.</summary>
        bool HoldCursorPressed { get; }

        /// <summary>Pressed this frame: the special module (E).</summary>
        bool SpecialPressed { get; }
    }
}
