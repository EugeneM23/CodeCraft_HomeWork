using Atomic.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Where the mouse cursor points in the world. Aiming and grenades take the point on a
    /// horizontal plane; the abilities cast a physics ray at the ground or at an entity.
    /// </summary>
    public static class CursorUseCase
    {
        /// <summary>
        /// Where on the screen the player aims. Usually the mouse cursor. While the cursor is
        /// locked (VoxelSandbox's mouse look), the crosshair in the middle of the screen. A locked
        /// cursor is not read at all: its reported position wanders with the mouse until it is
        /// pulled back to the centre.
        /// </summary>
        public static Vector3 ScreenPoint => Cursor.lockState == CursorLockMode.Locked
            ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
            : Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;

        /// <summary>
        /// The point where the ray under the cursor meets the horizontal plane at
        /// <paramref name="height"/>. False when the ray runs parallel to it or away from it.
        /// </summary>
        public static bool TryProjectOnPlane(Camera camera, float height, out Vector3 point)
        {
            point = default;

            Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            Ray ray = camera.ScreenPointToRay(ScreenPoint);

            if (!plane.Raycast(ray, out float distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }

        public static bool RayCastGround(out Vector3 worldPosition, LayerMask layerMask)
        {
            Ray ray = Camera.main.ScreenPointToRay(ScreenPoint);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
            {
                worldPosition = hit.point;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        public static bool RayCastTarget(out MonoEntity target, LayerMask layerMask)
        {
            target = null;

            Ray ray = Camera.main.ScreenPointToRay(ScreenPoint);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
            {
                if (hit.collider.TryGetComponent(out MonoEntity sceneEntity))
                {
                    target = sceneEntity;
                    return true;
                }
            }

            return false;
        }
    }
}
