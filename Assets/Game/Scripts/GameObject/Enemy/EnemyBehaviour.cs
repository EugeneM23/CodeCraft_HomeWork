using Gameplay;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyBehaviour : ITickable, IInitializeble
    {
        [Inject] private readonly CharacterController2D _character;
        [Inject] private readonly PlayerCharacterProvider _provider;
        [Inject] private readonly StateMachine _stateMachine;

        private readonly Transform[] _patrolPoints;
        private readonly float _chaseDistance = 10f;
        private readonly float _attackDistance = 2f;
        private readonly float _waypointReachDistance = 0.3f;

        private Vector2[] _patrolPositions;
        private int _currentPatrolIndex = 0;
        private bool _wasGrounded = true;

        public EnemyBehaviour(Transform[] patrolPoints)
        {
            _patrolPoints = patrolPoints;
        }

        public void Tick()
        {
            var distance = Vector2.Distance(_character.transform.position, _provider.Player.transform.position);

            // Проверяем потерю земли
            if (_wasGrounded && !_character.IsGrounded)
            {
                _character.Jump();
            }

            _wasGrounded = _character.IsGrounded;

            // Если в воздухе - только двигаемся
            if (!_character.IsGrounded)
            {
                if (distance < _chaseDistance)
                {
                    Vector2 direction = (_provider.Player.transform.position - _character.transform.position)
                        .normalized;
                    _character.Move(direction);
                }

                return;
            }

            // Атака
            if (distance <= _attackDistance)
            {
                _character.Move(Vector2.zero);
                _stateMachine.SetState<AttackState>();
                return;
            }

            // Преследование
            if (distance < _chaseDistance)
            {
                Vector2 direction = (_provider.Player.transform.position - _character.transform.position)
                    .normalized;
                _character.Move(direction);
                return;
            }

            // Патрулирование
            Patrol();
        }

        private void Patrol()
        {
            if (_patrolPositions == null || _patrolPositions.Length == 0)
            {
                _character.Move(Vector2.zero);
                return;
            }

            Vector2 targetPoint = _patrolPositions[_currentPatrolIndex];
            float distanceToWaypoint = Mathf.Abs(_character.transform.position.x - targetPoint.x);
            Vector2 direction = (targetPoint - (Vector2)_character.transform.position).normalized;

            if (distanceToWaypoint < _waypointReachDistance)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPositions.Length;
            }

            _character.Move(direction);
        }

        public void Initialize()
        {
            if (_patrolPoints != null && _patrolPoints.Length > 0)
            {
                _patrolPositions = new Vector2[_patrolPoints.Length];

                for (int i = 0; i < _patrolPoints.Length; i++)
                {
                    _patrolPositions[i] = _patrolPoints[i].position;
                }
            }
        }
    }
}