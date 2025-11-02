using Modules.PlayerController;

namespace Gameplay
{
    public class LandingState : BaseState
    {
        public LandingState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator,
            stateMachine, player)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.Landing).Interrupt(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt) 
                _stateMachine.SetState<IdleState>();
        }
    }
}