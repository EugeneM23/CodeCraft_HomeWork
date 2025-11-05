using Modules.PlayerController;

namespace Gameplay
{
    public class WallSlideState : BaseState
    {
        public override void Tick()
        {
            _animator.Play(AnimationID.WallSlide);

            if (!_character.IsWallSliding) 
                AnimationFsm.SetState<IdleState>();
        }
    }
}