using Modules.PlayerController;

namespace Gameplay
{
    public class WallSlideState : BaseState
    {
        public WallSlideState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator, stateMachine, player)
        {
        }

        public override void Tick()
        {
            _animator.Play(AnimationID.WallSlide);

            if (!_player.IsWallSliding) 
                _stateMachine.SetState<IdleState>();
        }
    }
}