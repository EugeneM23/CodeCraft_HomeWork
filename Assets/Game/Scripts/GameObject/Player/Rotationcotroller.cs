using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class Rotationcotroller : IInitializeble
    {
        private RotationComponent _rotationComponent;
        private InputReader _inputReader;

        [Inject]
        public void Construct(RotationComponent rotationComponent, InputReader inputReader)
        {
            _rotationComponent = rotationComponent;
            _inputReader = inputReader;
        }

        public void Initialize() => _inputReader.OnMove += Move;

        private void OnDisable() => _inputReader.OnMove -= Move;

        private void Move(Vector2 direction)
        {
            _rotationComponent.SetDiraction(direction);
        }
    }
}