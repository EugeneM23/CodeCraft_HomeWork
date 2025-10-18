using UnityEngine;

namespace Gameplay
{
    public class EnemyMoveController
    {
        private readonly MoveComponent _moveComponent;
        private readonly RotationComponent _rotationComponent;

        public EnemyMoveController(MoveComponent moveComponent, RotationComponent rotationComponent)
        {
            _moveComponent = moveComponent;
            _rotationComponent = rotationComponent;
        }

        public void Move(Vector2 direction)
        {
            _moveComponent.SetDirection(direction);
            _rotationComponent.SetDiraction(direction);
        }
    }
}