using System.Collections;
using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay.Ability
{
    public class PushAbility
    {
        private const float CAST_OFFSET = 1f;

        private LayerMask _layerMask;
        private SensorComponent _sensorComponent;

        private readonly float _radius = 2f;
        private readonly float _distance = 10f;

        private IAction[] _pushActions;
        private Transform _transform;

        public interface IAction
        {
            void Invoke();
        }

        [Inject]
        private void Construct(LayerMask layerMask, IAction[] pushActions, SensorComponent sensorComponent, Transform transform)
        {
            _transform = transform;
            _sensorComponent = sensorComponent;
            _layerMask = layerMask;
            _pushActions = pushActions;
        }

        public void Push(Vector2 impulseDirection)
        {
            var castDir = new Vector2(_transform.lossyScale.x, 0);
            RaycastHit2D[] hits = _sensorComponent.Sense(castDir, _distance, _radius, _layerMask);

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
}