using Gameplay;

namespace Game.Scripts.GameObject.Enemy
{
    public class StepSFXController : IInitializeble
    {
        [Inject] private readonly StepSFXComponent _stepSFXComponent;
        [Inject] private readonly SpriteAnimator _spriteAnimator;
        public void Initialize()
        {
            _spriteAnimator.OnEventRaised += PlayAudio;
        }

        private void PlayAudio(EventID id)
        {
            if (id == EventID.Step) 
                _stepSFXComponent.PlayStep();
        }
    }
}