using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class EnemyAttackComponent : IInitializeble, ITickable
    {
        private Transform _character;
        private EnemyMoveController _moveController;
        private Sensor _sensor;
        private StateMachine _stateMachine;
        private readonly LayerMask _targetLayer;

        private Transform _currentTarget;
        private readonly float _reachThreshold = 2f;
        private Vector2 _lookDirection;

        public bool HasTarget => _currentTarget != null;

        [Inject]
        private void Construct(
            Transform character,
            EnemyMoveController moveController,
            Sensor sensor,
            StateMachine stateMachine
        )
        {
            _character = character;
            _moveController = moveController;
            _sensor = sensor;
            _stateMachine = stateMachine;
        }

        public EnemyAttackComponent(LayerMask targetLayer)
        {
            _targetLayer = targetLayer;
        }

        public void Initialize()
        {
            _currentTarget = null;
        }

        public void Tick()
        {
            _lookDirection = new Vector2(_character.localScale.x, 0);
            RaycastHit2D[] hits = _sensor.Sense(_lookDirection, 6f, 1f, _targetLayer, true);

            if (hits != null && hits.Length > 0)
                _currentTarget = hits[0].transform;
            else
                _currentTarget = null;

            if (_currentTarget != null)
            {
                MoveToTarget(_currentTarget);
            }
            else
                _moveController.Move(Vector3.zero);
        }

        private void MoveToTarget(Transform target)
        {
            Vector3 direction = target.position - _character.position;
            direction.y = 0;

            float distance = direction.magnitude;

            if (distance < _reachThreshold)
            {
                _stateMachine.SetState<AttackState>();
                _moveController.Move(Vector3.zero);
                return;
            }

            direction.Normalize();
            _moveController.Move(direction);
        }
    }
}