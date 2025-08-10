using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Game
{
    public class InputReader : IInitializable, GameInput.IMovementActions,
        IGameOverListener, ILevelStartListener, IGameWinListener
    {
        public event Action<Vector2> OnMove;

        private readonly GameInput _gameInput = new();

        public void Initialize()
        {
            _gameInput.Movement.SetCallbacks(this);
            _gameInput.Movement.Enable();
        }

        private void Move(Vector2 direction) => OnMove?.Invoke(direction);
        public void OnWASD(InputAction.CallbackContext context) => Move(context.ReadValue<Vector2>());
        public void OnARROW(InputAction.CallbackContext context) => Move(context.ReadValue<Vector2>());

        public void FinishGame() => _gameInput.Movement.Disable();

        public void StartLevel() => _gameInput.Movement.Enable();

        public void WinGame() => _gameInput.Movement.Disable();
    }
}