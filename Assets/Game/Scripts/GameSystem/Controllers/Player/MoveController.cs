using UnityEngine;

namespace Gameplay
{
    public class MoveController : IInitializeble, IDisposable
    {
        [Inject] private readonly InputReader _inputReader;
        private readonly MoveComponent _moveComponent;
        private readonly RotationComponent _rotationComponent;

        public MoveController(MoveComponent moveComponent, RotationComponent rotationComponent)
        {
            _moveComponent = moveComponent;
            _rotationComponent = rotationComponent;
        }

        public void Initialize()
        {
            _inputReader.OnMove += Move;
        }

        public void Dispose()
        {
            _inputReader.OnMove -= Move;
        }

        private void Move(Vector2 direction)
        {
            _moveComponent.SetDirection(direction);
            _rotationComponent.SetDiraction(direction);
        }
    }
}