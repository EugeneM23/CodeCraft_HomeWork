using Modules.PlayerController;

namespace Gameplay
{
    public class RunToIdleState : BaseState
    {
        public override void Enter() => _animator.Play(AnimationID.RunToIdle).CanBreak(false);

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                AnimationFsm.SetState<IdleState>();
        }
    }
}