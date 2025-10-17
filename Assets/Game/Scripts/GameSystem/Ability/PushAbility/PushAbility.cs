using System.Collections;
using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay.Ability
{
    public class PushAbility
    {
        private const float CAST_OFFSET = 1f;

        private Transform _transform;
        private LayerMask _layerMask;

        private readonly float _radius = 2f;
        private readonly float _distance = 10f;
        private IAction[] _pushActions;

        private readonly PushDebugDrawer _debugDrawer = new();

        [Inject]
        private void Construct(Transform transform, LayerMask layerMask, IAction[] pushActions)
        {
            _transform = transform;
            _layerMask = layerMask;
            _pushActions = pushActions;
        }

        public interface IAction
        {
            void Invoke();
        }

        public PushAbility(LayerMask layerMask, Transform transform)
        {
            _layerMask = layerMask;
        }

        public void Push(Vector2 direction)
        {
            RayCast(out var origin, out var dir, out var hits);

            _debugDrawer.Draw(origin, dir, _distance, _radius, Color.green);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.TryGetComponent(out Entity entity) &&
                    entity.TryGetEntityComponent<ImpulseComponent>(out var impulseComponent))
                {
                    impulseComponent.AddForce(direction, 40f);
                    _debugDrawer.Draw(origin, dir, _distance, _radius, Color.red);
                }
            }

            DoActions();
        }

        private void RayCast(out Vector2 origin, out Vector2 dir, out RaycastHit2D[] hits)
        {
            origin = new Vector2(_transform.position.x, _transform.position.y + CAST_OFFSET);
            dir = _transform.localScale;
            dir.y = 0;
            hits = Physics2D.CircleCastAll(origin, _radius, dir, _distance, _layerMask);
        }

        private void DoActions()
        {
            foreach (IAction action in _pushActions)
                action.Invoke();
        }
    }
}