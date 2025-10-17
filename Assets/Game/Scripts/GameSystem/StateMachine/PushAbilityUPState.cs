namespace Gameplay
{
    public class PushAbilityUPState : IState
    {
        [Inject] private readonly SpriteAnimator _animator;

        public void Enter()
        {
            _animator.Play(AnimationID.PushAbilityUP).Interrupt(false);
        }
    }
}