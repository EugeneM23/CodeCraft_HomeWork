using UnityEngine;

namespace Gameplay
{
    public class MoveController : IInitializeble, IDisposable
    {
        [Inject] private IMovabele _moveComponent;
        [Inject] private RotationComponent _rotationComponent;
        [Inject] private InputReader _inputReader;

        public void Construct(IMovabele moveComponent, RotationComponent rotationComponent, InputReader inputReader)
        {
            _moveComponent = moveComponent;
            _rotationComponent = rotationComponent;
            _inputReader = inputReader;
        }

        public void Initialize() => _inputReader.OnMove += Move;

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