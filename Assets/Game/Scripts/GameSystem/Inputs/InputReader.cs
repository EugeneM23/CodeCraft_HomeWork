using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    public class InputReader : GameInput.IMovementActions, GameInput.IJumpActions, GameInput.IFireActions,
        IInitializeble
    {
        public event Action<Vector2> OnMove;
        public event Action OnJump;
        public event Action OnFire;

        private GameInput _gameInput;

        public void Initialize()
        {
            _gameInput = new GameInput();

            _gameInput.Movement.Enable();
            _gameInput.Movement.AddCallbacks(this);

            _gameInput.Jump.Enable();
            _gameInput.Jump.AddCallbacks(this);

            _gameInput.Fire.Enable();
            _gameInput.Fire.AddCallbacks(this);
        }

        private void OnDisable()
        {
            _gameInput.Movement.Disable();
            _gameInput.Movement.RemoveCallbacks(this);

            _gameInput.Jump.Disable();
            _gameInput.Jump.RemoveCallbacks(this);

            _gameInput.Fire.Disable();
            _gameInput.Fire.RemoveCallbacks(this);
        }

        void GameInput.IMovementActions.OnWASD(InputAction.CallbackContext context)
        {
            OnMove?.Invoke(context.ReadValue<Vector2>());
        }

        void GameInput.IJumpActions.OnJump(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started) 
                OnJump?.Invoke();
        }

        void GameInput.IFireActions.Fire(InputAction.CallbackContext context)
        {
            OnFire?.Invoke();
        }
    }
}