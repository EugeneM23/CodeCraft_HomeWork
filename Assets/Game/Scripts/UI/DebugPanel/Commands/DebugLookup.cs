using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What debug commands look up in the open level: the player's tank, its root, the point the camera looks at, the
    /// ground under a point. Read on a press, not cached: the level may have changed since.
    /// </summary>
    public static class DebugLookup
    {
        private const float AIM_DISTANCE = 400f;
        private const float FALLBACK_AHEAD = 15f;
        private static readonly RaycastHit[] s_hits = new RaycastHit[32];

        public static bool TryGetTank(out IEntity tank)
        {
            tank = null;
            if (!GameContext.TryGetInstance(out GameContext context) || !context.TryGetPlayerContext(out PlayerContext player)
                || player == null || !player.TryGetCharacter(out IReactiveVariable<IEntity> character))
                return false;

            tank = character.Value;
            return tank != null;
        }

        public static Transform RootOf(IEntity entity)
        {
            if (entity is Component component && component != null)
                return component.transform;

            return entity != null && entity.TryGetTransform(out Transform transform) ? transform : null;
        }

        /// <summary>
        /// Where the gameplay camera's centre ray meets the world (the tank itself skipped); without a hit, 15 m in
        /// front of the tank.
        /// </summary>
        public static bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            TryGetTank(out IEntity tank);
            Transform root = RootOf(tank);
            Camera camera = Camera.main;
            if (camera != null)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                int count = Physics.RaycastNonAlloc(ray, s_hits, AIM_DISTANCE, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = s_hits[i];
                    if (hit.distance >= best || (root != null && hit.transform.IsChildOf(root)))
                        continue;
                    best = hit.distance;
                    point = hit.point;
                }

                if (best < float.MaxValue)
                    return true;
            }

            if (root == null)
                return false;

            Vector3 ahead = root.forward;
            ahead.y = 0f;
            point = Ground(root.position + ahead.normalized * FALLBACK_AHEAD);
            return true;
        }

        /// <summary>The highest solid surface under the point (a ray from 40 m above), or the point itself.</summary>
        public static Vector3 Ground(Vector3 point)
        {
            Vector3 from = new(point.x, point.y + 40f, point.z);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }
    }
}
