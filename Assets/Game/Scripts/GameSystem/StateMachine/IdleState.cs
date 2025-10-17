
namespace Gameplay
{
    public class IdleState : IState
    {
        private readonly SpriteAnimator _animator;
        public IdleState(SpriteAnimator animator) => _animator = animator;

        public void Enter() => _animator.Play(AnimationID.Idle);
    }
}