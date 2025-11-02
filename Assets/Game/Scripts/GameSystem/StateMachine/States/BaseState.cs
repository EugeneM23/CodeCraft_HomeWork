using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public abstract class BaseState : IInitializeble, IDisposable
    {
        protected readonly SpriteAnimator _animator;
        protected readonly StateMachine _stateMachine;
        protected readonly PlayerController _player;

        protected BaseState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player)
        {
            _animator = animator;
            _stateMachine = stateMachine;
            _player = player;
        }

        void IInitializeble.Initialize()
        {
            Debug.Log($"Initializing {GetType().Name}");
            _player.OnDash += TransitionToDash;
            _player.OnJump += TransitionToJump;
        }

        void IDisposable.Dispose()
        {
            _player.OnDash -= TransitionToDash;
            _player.OnJump -= TransitionToJump;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void Tick()
        {
            if (_player.IsWallSliding)
            {
                _stateMachine.SetState<WallSlideState>();
                return;
            }

            if (!_player.IsGrounded)
            {
                _stateMachine.SetState<FallMidState>();
                return;
            }
        }

        private void TransitionToJump() => _stateMachine.SetState<RiseState>();

        private void TransitionToDash() => _stateMachine.SetState<DashState>();
    }
}