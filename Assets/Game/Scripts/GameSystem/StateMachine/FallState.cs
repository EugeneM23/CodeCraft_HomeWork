using Game.Scripts.Modules.SpriteAnimator;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
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