namespace Gameplay
{
    public class ThrowItemState : BaseState
    {
        public override void Enter()
        {
            _animator.PlayForce(AnimationID.ThrowItem).CanBreak(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<IdleState>();
        }
    }
}