using System.Collections;
using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay.Ability
{
    public class PushAbility
    {
        private const float DEBUG_DURATION = 0.1f;
        private const float CAST_OFFSET = 1f;

        private readonly Transform _transform;
        private readonly LayerMask _layerMask;

        private float _radius = 0.5f;
        private float _distance = 10f;

        [Inject] private IAction[] _pushActions;

        public interface IAction
        {
            void Invoke();
        }

        public PushAbility(LayerMask layerMask, Transform transform)
        {
            _transform = transform;
            _layerMask = layerMask;
        }

        public void Push(Vector2 direction)
        {
            Vector2 origin = new Vector2(_transform.position.x, _transform.position.y + CAST_OFFSET);
            Vector2 dir = _transform.localScale;
            dir.y = 0;

            RaycastHit2D hit = Physics2D.CircleCast(origin, _radius, dir, _distance, _layerMask);
            DrawDebug(origin, dir, Color.green);

            if (hit.collider == null) return;

            if (hit.collider.TryGetComponent(out Entity entity))
            {
                if (entity.TryGetEntityComponent<ImpulseComponent>(out var impulseComponent))
                {
                    impulseComponent.AddForce(direction, 40f);
                    foreach (var item in _pushActions) item.Invoke();
                }

                DrawDebug(origin, dir, Color.red);
            }
        }

        private void DrawDebug(Vector2 origin, Vector2 dir, Color color)
        {
            Vector2 end = origin + dir * _distance;
            Debug.DrawLine(origin, end, color, DEBUG_DURATION);
            DrawCircle(origin, _radius, color, DEBUG_DURATION);
            DrawCircle(end, _radius, color, DEBUG_DURATION);
        }

        void DrawCircle(Vector2 center, float radius, Color color, float duration)
        {
            int segments = 20;
            float angle = 0f;
            Vector2 lastPoint = center + new Vector2(radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 newPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Debug.DrawLine(lastPoint, newPoint, color, duration);
                lastPoint = newPoint;
            }
        }
    }

    public class TestPushAction01 : PushAbility.IAction
    {
        public void Invoke()
        {
            Debug.Log("Action: TestPushAction01");
        }
    }

    public class TestPushAction02 : PushAbility.IAction
    {
        public void Invoke()
        {
            Debug.Log("Action: TestPushAction02");
        }
    }
}