using System;
using UnityEngine;

namespace Gameplay
{
    public class MoveController : IInitializeble
    {
        private MoveComponent _moveComponent;
        private RotationComponent _rotationComponent;

        public void Initialize()
        {
            _moveComponent = ServiceLocator.Get<MoveComponent>(PlayerId.MoveComponent);
            _rotationComponent = ServiceLocator.Get<RotationComponent>(PlayerId.RotationComponent);
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnMove += Move;
        }

        private void OnDisable()
        {
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnMove -= Move;
        }

        private void Move(Vector2 direction)
        {
            _moveComponent.SetDirection(direction);
            _rotationComponent.SetDiraction(direction);
        }
    }
}