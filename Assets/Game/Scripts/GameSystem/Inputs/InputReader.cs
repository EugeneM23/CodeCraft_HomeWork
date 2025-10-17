using System;
using Gamplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    public class InputReader :
        GameInput.IMovementActions,
        GameInput.IJumpActions,
        GameInput.IFireActions,
        IInitializeble,
        IDisposable,
        GameInput.IPushAbilityUpActions,
        GameInput.IPushAbilitySideActions
    {
        public event Action<Vector2> OnMove;
        public event Action OnJump;
        public event Action OnPushUp;
        public event Action OnPushSide;
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

            _gameInput.PushAbilityUp.Enable();
            _gameInput.PushAbilityUp.AddCallbacks(this);

            _gameInput.PushAbilitySide.Enable();
            _gameInput.PushAbilitySide.AddCallbacks(this);
        }

        public void Dispose()
        {
            _gameInput.Movement.Disable();
            _gameInput.Movement.RemoveCallbacks(this);

            _gameInput.Jump.Disable();
            _gameInput.Jump.RemoveCallbacks(this);

            _gameInput.Fire.Disable();
            _gameInput.Fire.RemoveCallbacks(this);

            _gameInput.PushAbilityUp.Disable();
            _gameInput.PushAbilityUp.RemoveCallbacks(this);

            _gameInput.PushAbilitySide.Disable();
            _gameInput.PushAbilitySide.RemoveCallbacks(this);
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

        void GameInput.IFireActions.OnFire(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
                OnFire?.Invoke();
        }

        public void OnPushAbilityUp(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
                OnPushUp?.Invoke();
        }

        public void OnPushAbilitySide(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
                OnPushSide?.Invoke();
        }
    }
}