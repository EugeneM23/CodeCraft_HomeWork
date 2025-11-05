
namespace Gameplay
{
    public class SmashState : BaseState
    {
        public override void Enter()
        {
            _animator.Play(AnimationID.Smash).CanBreak(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                AnimationFsm.SetState<IdleState>();
        }
    }
}