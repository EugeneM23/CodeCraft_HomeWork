using Modules.PlayerController;

namespace Gameplay
{
    public class SmashState : BaseState
    {
        public SmashState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character) : base(
            animator,
            stateMachine, character)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.Smash).Interrupt(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<IdleState>();
        }
    }
}