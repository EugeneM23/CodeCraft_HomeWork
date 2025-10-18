using UnityEngine;

namespace Gameplay.Ability
{
    public class SensorComponent
    {
        private readonly float _castOffset = 1f;
        private readonly PushDebugDrawer _debugDrawer = new();

        private Transform _transform;

        [Inject]
        private void Construct(Transform transform)
        {
            _transform = transform;
        }

        public RaycastHit2D[] Sense(Vector2 direction, float distance, float radius, LayerMask layerMask,
            bool drawDebug = true)
        {
            Vector2 origin = new Vector2(_transform.position.x, _transform.position.y + _castOffset);
            Vector2 dir = direction.normalized;

            var hits = Physics2D.CircleCastAll(origin, radius, dir, distance, layerMask);

            if (drawDebug)
                _debugDrawer.Draw(origin, dir, distance, radius, Color.green);

            return hits;
        }
    }
}