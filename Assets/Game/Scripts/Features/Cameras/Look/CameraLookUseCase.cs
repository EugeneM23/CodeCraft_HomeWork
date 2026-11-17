using UnityEngine;

namespace Game
{
    /// <summary>
    /// The camera leans towards the cursor so the player sees more of where they are looking.
    ///
    /// The lean is taken from where the cursor sits on the screen, not from the point it covers in
    /// the world. A world point moves whenever the camera does, so the camera would keep drifting
    /// while the mouse stood still; a screen position does not.
    /// </summary>
    public static class CameraLookUseCase
    {
        /// <summary>
        /// Cursor on the screen → look on the screen: a direction times a strength from 0 to 1.
        /// Nothing inside the dead zone around the centre, then linear up to 1 at the edge.
        /// A cursor outside the window counts as being on its edge.
        /// </summary>
        public static Vector2 FromCursor(Vector2 cursor, Vector2 screenSize, float deadZone)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f) return Vector2.zero;

            Vector2 normalized = new(
                Mathf.Clamp(cursor.x / screenSize.x * 2f - 1f, -1f, 1f),
                Mathf.Clamp(cursor.y / screenSize.y * 2f - 1f, -1f, 1f));

            // The corners would otherwise lean √2 times further than the edges.
            normalized = Vector2.ClampMagnitude(normalized, 1f);

            float length = normalized.magnitude;
            deadZone = Mathf.Clamp(deadZone, 0f, 0.99f);

            if (length <= deadZone) return Vector2.zero;

            float strength = (length - deadZone) / (1f - deadZone);
            return normalized / length * strength;
        }

        /// <summary>
        /// How much more ground a tilted camera shows above the screen centre than below it.
        ///
        /// A camera pitched θ below the horizon with a vertical half-angle α sees, on flat ground,
        /// sin(θ + α) / sin(θ − α) times as far ahead of the centre as behind it: 1 looking straight
        /// down, about 1.25 for the 73° / 40° camera of the playground. When the top edge reaches
        /// the horizon the ground ahead never ends, so the ratio is capped.
        /// </summary>
        public static float BackwardBoost(Vector3 cameraForward, float verticalFieldOfView)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(-cameraForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float halfAngle = verticalFieldOfView * 0.5f;

            float below = Mathf.Sin((pitch - halfAngle) * Mathf.Deg2Rad);
            if (below <= 0f) return MaxBackwardBoost;

            float above = Mathf.Sin((pitch + halfAngle) * Mathf.Deg2Rad);
            return Mathf.Clamp(above / below, 1f, MaxBackwardBoost);
        }

        /// <summary>
        /// Evens out the tilt: a look down the screen — behind the character, where less ground is
        /// on show — reaches further, by the backward boost. A look up the screen is left as it is.
        /// </summary>
        public static Vector2 CompensateTilt(Vector2 look, float backwardBoost)
        {
            if (look.y < 0f) look.y *= backwardBoost;
            return look;
        }

        private const float MaxBackwardBoost = 2f;

        /// <summary>
        /// Look on the screen → look on the ground: screen right is the camera's right, screen up is
        /// the way the camera faces. The camera's up is used rather than its forward, because a
        /// camera looking almost straight down has a forward whose ground shadow is close to nothing.
        /// </summary>
        public static Vector3 ToGround(Vector2 look, Vector3 cameraRight, Vector3 cameraUp)
        {
            Vector3 right = Flatten(cameraRight, Vector3.right);
            Vector3 forward = Flatten(cameraUp, Vector3.forward);
            return right * look.x + forward * look.y;
        }

        /// <summary>
        /// Where the camera root sits relative to the camera point. The whole offset grows with the
        /// distance and, by up to MaxZoom, with the look; the look then shifts it along the ground.
        /// A look behind the character may be longer than 1 after CompensateTilt: it shifts that much
        /// further, while the pull-back never goes past MaxZoom.
        /// </summary>
        public static Vector3 Offset(Vector3 baseOffset, float distance, Vector3 look, CameraLookSettings settings)
        {
            float pullBack = 1f + settings.MaxZoom * Mathf.Clamp01(look.magnitude);

            return baseOffset * (distance * pullBack) + look * (settings.MaxShift * distance);
        }

        private static Vector3 Flatten(Vector3 axis, Vector3 fallback)
        {
            axis.y = 0f;
            return axis.sqrMagnitude > 1e-6f ? axis.normalized : fallback;
        }
    }
}
