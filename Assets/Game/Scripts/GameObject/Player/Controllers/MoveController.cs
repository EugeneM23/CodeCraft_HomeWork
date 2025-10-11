using UnityEngine;

namespace Gameplay
{
    public class MoveController : IInitializeble
    {
        private MoveComponent _moveComponent;
        private RotationComponent _rotationComponent;
        private InputReader _inputReader;

        public void Construct(MoveComponent moveComponent, RotationComponent rotationComponent, InputReader inputReader)
        {
            _moveComponent = moveComponent;
            _rotationComponent = rotationComponent;
            _inputReader = inputReader;
        }

        public void Initialize() => _inputReader.OnMove += Move;

        private void OnDisable() => _inputReader.OnMove -= Move;

        private void Move(Vector2 direction)
        {
            _moveComponent.SetDirection(direction);
            _rotationComponent.SetDiraction(direction);
        }
    }
}