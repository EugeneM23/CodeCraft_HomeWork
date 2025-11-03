
namespace Gameplay
{
    public class SmashState : BaseState
    {
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