using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class AnimationInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animations;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private CharacterController2D character;

        public override void Install(DiContainer container)
        {
            Application.targetFrameRate = 140;
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer, receiver);

            container.BindSingle(character);
            container.BindInterfacesAndSelf(spriteAnimator);
            container.BindSingle(receiver);

            var stateMachine = new StateMachine();
            container.BindInterfacesAndSelf(stateMachine);

            container.BindInterfacesAndSelf(new IdleState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new RunState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new JumpStartState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new FallMidState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new LandingState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new RunToIdleState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new DashState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new WallSlideState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new RollState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new FrontFlipState(spriteAnimator, stateMachine, character));
            container.BindInterfacesAndSelf(new SmashState(spriteAnimator, stateMachine, character));
        }
    }
}