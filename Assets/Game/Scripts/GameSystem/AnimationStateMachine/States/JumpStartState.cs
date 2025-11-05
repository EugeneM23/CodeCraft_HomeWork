namespace Gameplay
{
    public class JumpStartState : BaseState
    {
        public override void Enter() => _animator.PlayForce(AnimationID.JumpStart).CanBreak(false);

        public override void Tick()
        {
            base.Tick();
            
            if (_animator.CurrentAnimation.CanInterrupt)
                AnimationFsm.SetState<FallMidState>();
        }
    }
}