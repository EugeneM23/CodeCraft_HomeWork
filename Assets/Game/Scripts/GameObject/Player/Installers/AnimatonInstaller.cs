using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class AnimationInstaller : Installer
    {
        [Header("Animation Settings")] [SerializeField]
        private SpriteAnimation[] _animations;

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private PlayerController _player;

        public override void Install(DiContainer container)
        {
            // --- Core animation setup ---
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer, receiver);

            container.BindSingle(_player);
            container.BindSingle(spriteAnimator);
            container.BindSingle(receiver);


            // --- State machine ---
            var stateMachine = new StateMachine();
            container.BindSingle(stateMachine);

            // --- Animation states ---
            container.BindInterface<BaseState>(new IdleState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new RunState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new RiseState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new FallState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new LandingState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new RunToIdleState(spriteAnimator, stateMachine, _player));
            container.BindInterface<BaseState>(new DashState(spriteAnimator, stateMachine, _player));
        }
    }
}