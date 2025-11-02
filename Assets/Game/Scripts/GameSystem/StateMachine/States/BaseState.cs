using Modules.PlayerController;

namespace Gameplay
{
    public abstract class BaseState : IInitializeble, IDisposable
    {
        protected readonly SpriteAnimator _animator;
        protected readonly StateMachine _stateMachine;
        protected readonly CharacterController2D Character;

        protected BaseState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character)
        {
            _animator = animator;
            _stateMachine = stateMachine;
            Character = character;
        }

        void IInitializeble.Initialize()
        {
            Character.OnDash += TransitionToDash;
            Character.OnJump += TransitionToJump;
            Character.OnSmash += TransitionToSmash;
        }

        private void TransitionToSmash()
        {
            _stateMachine.SetState<SmashState>();
        }

        void IDisposable.Dispose()
        {
            Character.OnDash -= TransitionToDash;
            Character.OnJump -= TransitionToJump;
            Character.OnSmash -= TransitionToSmash;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void Tick()
        {
            if (Character.IsWallSliding)
            {
                _stateMachine.SetState<WallSlideState>();
                return;
            }

            if (!Character.IsGrounded)
            {
                _stateMachine.SetState<FallMidState>();
                return;
            }
        }

        private void TransitionToJump()
        {
            _stateMachine.SetState<JumpStartState>();
        }

        private void TransitionToDash()
        {
            if (Character.IsGrounded)
                _stateMachine.SetState<RollState>();
            else
                _stateMachine.SetState<DashState>();
        }
    }
}