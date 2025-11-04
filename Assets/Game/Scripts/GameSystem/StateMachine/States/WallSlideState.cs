using Modules.PlayerController;

namespace Gameplay
{
    public class WallSlideState : BaseState
    {
        public override void Tick()
        {
            _animator.Play(AnimationID.WallSlide);

            if (!_contoller.IsWallSliding) 
                _stateMachine.SetState<IdleState>();
        }
    }
}