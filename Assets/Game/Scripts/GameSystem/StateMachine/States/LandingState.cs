using Modules.PlayerController;

namespace Gameplay
{
    public class LandingState : BaseState
    {
        public override void Enter()
        {
            _animator.Play(AnimationID.Landing).CanBreak(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<IdleState>();
        }
    }
}