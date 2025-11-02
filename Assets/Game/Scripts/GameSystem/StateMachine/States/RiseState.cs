using Modules.PlayerController;

namespace Gameplay
{
    public class RiseState : BaseState
    {
        public RiseState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.JumpRise).Interrupt(false);
        }

        public override void Tick()
        {
            base.Tick();
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<FallMidState>();
        }
    }
}