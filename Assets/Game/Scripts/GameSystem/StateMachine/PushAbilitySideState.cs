namespace Gameplay
{
    public class PushAbilitySideState : IState
    {
        [Inject] private readonly SpriteAnimator _animator;

        public void Enter()
        {
            _animator.Play(AnimationID.PushAbilitySide).Interrupt(false);
        }
    }
}