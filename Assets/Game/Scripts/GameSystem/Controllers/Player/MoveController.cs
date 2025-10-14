using UnityEngine;

namespace Gameplay
{
    public class MoveController : IInitializeble, IDisposable
    {
        private MoveComponent _moveComponent;
        private RotationComponent _rotationComponent;
        private InputReader _inputReader;

        [Inject]
        public void Construct(MoveComponent moveComponent, RotationComponent rotationComponent, InputReader inputReader)
        {
            _moveComponent = moveComponent;
            _rotationComponent = rotationComponent;
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            Debug.Log("Initialize");
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