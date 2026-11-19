using UnityEngine;

namespace Game
{
    /// <summary>
    /// Where the gate pointer goes (pixels). A gate in front of the camera and inside the screen less the margin
    /// is marked where it is. Otherwise the arrow sits where the ray from the tank's screen point, along the
    /// gate's direction on screen, leaves that inner rectangle, turned that way (degrees, 0 = right,
    /// counter-clockwise). The direction comes from the world vector gate − tank (arenas lie far from the
    /// origin), projected to the screen near the tank, so a gate behind the camera still points the right way.
    /// </summary>
    public static class GatePointerUseCase
    {
        /// <summary>
        /// gateScreen is Camera.WorldToScreenPoint of the gate (z negative behind); tankScreen the tank's screen
        /// point; direction the gate's direction on screen from the tank. Returns true when the gate is on screen.
        /// </summary>
        public static bool Place(Vector3 gateScreen, Vector2 tankScreen, Vector2 direction, Vector2 screenSize, float margin,
            out Vector2 position, out float angle)
        {
            float minX = Mathf.Min(margin, screenSize.x * 0.5f);
            float minY = Mathf.Min(margin, screenSize.y * 0.5f);
            float maxX = screenSize.x - minX;
            float maxY = screenSize.y - minY;

            if (gateScreen.z > 0f && gateScreen.x >= minX && gateScreen.x <= maxX && gateScreen.y >= minY && gateScreen.y <= maxY)
            {
                position = new Vector2(gateScreen.x, gateScreen.y);
                angle = -90f; // a marker over the gate points down at it
                return true;
            }

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.down;

            Vector2 origin = new(Mathf.Clamp(tankScreen.x, minX, maxX), Mathf.Clamp(tankScreen.y, minY, maxY));
            float tX = direction.x > 0f ? (maxX - origin.x) / direction.x
                : direction.x < 0f ? (minX - origin.x) / direction.x : float.MaxValue;
            float tY = direction.y > 0f ? (maxY - origin.y) / direction.y
                : direction.y < 0f ? (minY - origin.y) / direction.y : float.MaxValue;
            position = origin + direction * Mathf.Min(tX, tY);
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return false;
        }
    }
}
