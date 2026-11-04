using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterMoveBehaviour : IEntityInit, IEntityTick
    {
        private IExpression<bool> _moveCondition;
        private IValue<Vector3> _moveDirection;

        public void Init(IEntity entity)
        {
            _moveCondition = entity.GetMoveCondition();
            _moveDirection = entity.GetMoveDirection();
        }

        public void Tick(IEntity entity, float deltaTime)
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