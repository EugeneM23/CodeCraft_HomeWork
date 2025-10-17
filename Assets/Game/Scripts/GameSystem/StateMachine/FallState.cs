
namespace Gameplay
{
    public class FallState : IState
    {
        private readonly SpriteAnimator _animator;

        public FallState(SpriteAnimator spriteAnimator)
        {
            _animator = spriteAnimator;
        }

        public void Enter() => _animator.Play(AnimationID.Fall);
        
    }
}