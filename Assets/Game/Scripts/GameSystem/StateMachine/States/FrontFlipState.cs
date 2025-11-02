using Modules.PlayerController;

namespace Gameplay
{
    public class FrontFlipState : BaseState
    {
        public FrontFlipState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator,
            stateMachine, player)
        {
        }

        public override void Enter() => _animator.Play(AnimationID.FrontFlip).Interrupt(false);

        public override void Tick()
        {
            base.Tick();
            if (_animator.CurrentAnimation.CanInterrupt || _player.IsGrounded || _player.IsOnWall)
                _stateMachine.SetState<FallMidState>();
        }
    }
}