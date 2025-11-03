using Modules.PlayerController;

namespace Gameplay
{
    public class FrontFlipState : BaseState
    {
        public override void Enter() => _animator.Play(AnimationID.FrontFlip).Interrupt(false);

        public override void Tick()
        {
            base.Tick();
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<FallMidState>();
        }
    }
}