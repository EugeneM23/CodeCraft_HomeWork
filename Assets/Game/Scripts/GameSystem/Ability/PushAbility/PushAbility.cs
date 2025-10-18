using System.Collections;
using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay.Ability
{
    public class PushAbility
    {
        private const float CAST_OFFSET = 1f;

        private LayerMask _layerMask;
        private Sensor _sensor;

        private readonly float _radius = 2f;
        private readonly float _distance = 10f;

        private IAction[] _pushActions;
        private Transform _transform;

        public interface IAction
        {
            void Invoke();
        }

        [Inject]
        private void Construct(LayerMask layerMask, IAction[] pushActions, Sensor sensor, Transform transform)
        {
            _transform = transform;
            _sensor = sensor;
            _layerMask = layerMask;
            _pushActions = pushActions;
        }

        public void Push(Vector2 impulseDirection)
        {
            var castDir = new Vector2(_transform.lossyScale.x, 0);
            RaycastHit2D[] hits = _sensor.Sense(castDir, _distance, _radius, _layerMask);

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.TryGetComponent(out Entity entity) &&
                    entity.TryGetEntityComponent<ImpulseComponent>(out var impulseComponent))
                {
                    impulseComponent.AddForce(impulseDirection, Random.Range(35, 55));
                }
            }

            DoActions();
        }

        private void DoActions()
        {
            foreach (IAction action in _pushActions)
                action.Invoke();
        }
    }

    public class Sensor
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