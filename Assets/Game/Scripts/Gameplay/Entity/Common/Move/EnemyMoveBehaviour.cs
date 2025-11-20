using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyMoveBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<Vector3> _moveDirection;
        private IPlayerContext _target;
        private Transform _enemyTransform;

        public void Init(in IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
            _target = GameContext.Instance.GetPlayers()[0];
            _enemyTransform = entity.GetTransform();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            Vector3 moveDirection = _target.GetCharacter().GetTransform().position - _enemyTransform.position;
            _moveDirection.Value = moveDirection.normalized;
        }
    }
}