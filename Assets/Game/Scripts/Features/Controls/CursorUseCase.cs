using UnityEngine;

namespace Game
{
    /// <summary>
    /// Where the player's pointer points in the world: aiming takes the point on a horizontal plane.
    /// </summary>
    public static class CursorUseCase
    {
        /// <summary>
        /// Where on the screen the player aims: <paramref name="pointer"/> (IPlayerInput.Pointer), or,
        /// while the cursor is locked (VoxelSandbox's mouse look), the crosshair in the middle of the
        /// screen. A locked cursor is not read at all: its reported position wanders with the mouse
        /// until it is pulled back to the centre.
        /// </summary>
        public static Vector2 ScreenPoint(Vector2 pointer) => Cursor.lockState == CursorLockMode.Locked
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : pointer;

        /// <summary>
        /// The point where the ray under <paramref name="screenPoint"/> meets the horizontal plane at
        /// <paramref name="height"/>. False when the ray runs parallel to it or away from it.
        /// </summary>
        public static bool TryProjectOnPlane(Camera camera, Vector2 screenPoint, float height, out Vector3 point)
        {
            point = default;

            Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            Ray ray = camera.ScreenPointToRay(screenPoint);

            if (!plane.Raycast(ray, out float distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }
    }
}
