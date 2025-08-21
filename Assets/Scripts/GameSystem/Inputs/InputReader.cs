using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Game
{
    public class InputReader : IInitializable, GameInput.IMovementActions,
        IGameOverListener, IGameStartListener, IGameWinListener
    {
        public event Action<Vector2> OnMove;

        private readonly GameInput _gameInput = new();

        public void Initialize()
        {
            _gameInput.Movement.SetCallbacks(this);
            _gameInput.Movement.Enable();
        }

        public void OnWasd(InputAction.CallbackContext context) => OnMove?.Invoke(context.ReadValue<Vector2>());
        public void OnArrow(InputAction.CallbackContext context) => OnMove?.Invoke(context.ReadValue<Vector2>());

        public void GameOver() => _gameInput.Movement.Disable();

        public void StartGame() => _gameInput.Movement.Enable();

        public void WinGame() => _gameInput.Movement.Disable();
    }
}