using Gameplay;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyBehaviour : ITickable, IInitializeble
    {
        [Inject] private readonly Entity _entity;
        [Inject] private readonly PlayerCharacterProvider _provider;

        private CharacterController2D _controller;
        private AnimationFSM _animationFsm;
        private JumpComponent _jumpComponent;

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

        public void Initialize()
        {
            GetPatrolPoints();

            _controller = _entity.GetEntityComponent<CharacterController2D>();
            _animationFsm = _entity.GetEntityComponent<AnimationFSM>();
            _jumpComponent = _entity.GetEntityComponent<JumpComponent>();
        }

        public void Tick()
        {
            var distance = Vector2.Distance(_controller.transform.position, _provider.Player.Transfrom.position);

            // Проверяем потерю земли
            if (_wasGrounded && !_controller.IsGrounded)
            {
                _jumpComponent.Jump();
            }

            _wasGrounded = _controller.IsGrounded;

            // Если в воздухе - только двигаемся
            if (!_controller.IsGrounded)
            {
                if (distance < _chaseDistance)
                {
                    Vector2 direction = (_provider.Player.Transfrom.position - _controller.transform.position)
                        .normalized;
                    _controller.SetMoveDirection(direction);
                }

                return;
            }

            // Атака
            if (distance <= _attackDistance)
            {
                _controller.SetMoveDirection(Vector2.zero);
                _animationFsm.SetState<AttackState>();
                return;
            }

            // Преследование
            if (distance < _chaseDistance)
            {
                Vector2 direction = (_provider.Player.Transfrom.position - _controller.transform.position)
                    .normalized;
                _controller.SetMoveDirection(direction);
                return;
            }

            // Патрулирование
            Patrol();
        }

        private void Patrol()
        {
            if (_patrolPositions == null || _patrolPositions.Length == 0)
            {
                _controller.SetMoveDirection(Vector2.zero);
                return;
            }

            Vector2 targetPoint = _patrolPositions[_currentPatrolIndex];
            float distanceToWaypoint = Mathf.Abs(_controller.transform.position.x - targetPoint.x);
            Vector2 direction = (targetPoint - (Vector2)_controller.transform.position).normalized;

            if (distanceToWaypoint < _waypointReachDistance)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPositions.Length;
            }

            _controller.SetMoveDirection(direction);
        }

        private void GetPatrolPoints()
        {
            if (_patrolPoints != null && _patrolPoints.Length > 0)
            {
                _patrolPositions = new Vector2[_patrolPoints.Length];

                for (int i = 0; i < _patrolPoints.Length; i++)
                    _patrolPositions[i] = _patrolPoints[i].position;
            }
        }
    }
}