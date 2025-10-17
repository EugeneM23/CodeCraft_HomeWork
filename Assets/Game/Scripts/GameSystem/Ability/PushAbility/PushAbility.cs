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

        private readonly float _radius = 0.5f;
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
            Vector2 origin = new Vector2(_transform.position.x, _transform.position.y + CAST_OFFSET);
            Vector2 dir = _transform.localScale;
            dir.y = 0;
            
            _debugDrawer.Draw(origin, dir, _distance, _radius, Color.green);

            RaycastHit2D hit = Physics2D.CircleCast(origin, _radius, dir, _distance, _layerMask);

            if (hit.collider == null) return;

            if (hit.collider.TryGetComponent(out Entity entity) &&
                entity.TryGetEntityComponent<ImpulseComponent>(out var impulseComponent))
            {
                impulseComponent.AddForce(direction, 40f);
                foreach (var action in _pushActions)
                    action.Invoke();

                _debugDrawer.Draw(origin, dir, _distance, _radius, Color.red);
            }
        }
    }
}