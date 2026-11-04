using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class RayCastUseCase
    {
        public static bool RayCastGround(out Vector3 worldPosition, LayerMask layerMask)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

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

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

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