using Modules.PlayerController;

namespace Gameplay
{
    public class JumpStartState : BaseState
    {
        public JumpStartState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Enter() => _animator.Play(AnimationID.JumpStart).Interrupt(false);

        public override void Tick()
        {
            base.Tick();
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<FallMidState>();
        }
    }
}