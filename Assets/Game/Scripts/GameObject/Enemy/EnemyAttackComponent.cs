using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class EnemyAttackComponent : IInitializeble, ITickable
    {
        private const float SenseDistance = 6f;
        private const float SenseRadius = 1f;
        private const float ReachThreshold = 2f;

        private LayerMask _targetLayer;
        private Transform _character;
        private EnemyMoveController _move;
        private SensorComponent _sensorComponent;
        private StateMachine _state;
        private Transform _target;

        public bool HasTarget => _target;

        [Inject]
        private void Construct(Transform character, EnemyMoveController move, SensorComponent sensorComponent, StateMachine state,
            LayerMask targetLayer)
        {
            _targetLayer = targetLayer;
            _character = character;
            _move = move;
            _sensorComponent = sensorComponent;
            _state = state;
        }

        public void Initialize() => _target = null;

        public void Tick()
        {
            var lookDir = new Vector2(_character.localScale.x, 0);
            var hits = _sensorComponent.Sense(lookDir, SenseDistance, SenseRadius, _targetLayer, true);

            _target = hits.Length > 0 ? hits[0].transform : null;

            if (!_target)
            {
                _move.Move(Vector3.zero);
                return;
            }

            var dir = _target.position - _character.position;
            dir.y = 0;

            if (dir.magnitude < ReachThreshold)
            {
                _state.SetState<AttackState>();
                _move.Move(Vector3.zero);
            }
            else
                _move.Move(dir.normalized);
        }
    }
}