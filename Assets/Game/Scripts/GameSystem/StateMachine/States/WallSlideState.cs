using Modules.PlayerController;

namespace Gameplay
{
    public class WallSlideState : BaseState
    {
        public WallSlideState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character) : base(
            animator, stateMachine, character)
        {
        }

        public override void Tick()
        {
            _animator.Play(AnimationID.WallSlide);

            if (!Character.IsWallSliding) 
                _stateMachine.SetState<IdleState>();
        }
    }
}