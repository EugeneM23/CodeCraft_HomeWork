using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterMoveBehaviour : IEntityInit, IEntityUpdate
    {
        private IExpression<bool> _moveCondition;
        private IValue<Vector3> _moveDirection;

        public void Init(in IEntity entity)
        {
            _moveCondition = entity.GetMoveCondition();
            _moveDirection = entity.GetMoveDirection();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (!_moveCondition.Value)
            {
                return;
            }

            Vector3 direction = _moveDirection.Value;
            MoveUseCase.Move(entity, direction, deltaTime);
        }
    }
}