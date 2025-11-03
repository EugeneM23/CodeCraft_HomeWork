using Gameplay;
using Modules.PlayerController;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class CharacterAnimationInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animations;
        [SerializeField] private SpriteRenderer _spriteRenderer;


        public override void Install(DiContainer container)
        {
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer, receiver);
            container.BindInterfacesAndSelf(spriteAnimator);
            container.BindSingle(receiver);

            var stateMachine = new StateMachine();
            container.BindInterfacesAndSelf(stateMachine);

            container.BindInterfacesAndSelf(new IdleState());
            container.BindInterfacesAndSelf(new RunState());
            container.BindInterfacesAndSelf(new JumpStartState());
            container.BindInterfacesAndSelf(new FallMidState());
            container.BindInterfacesAndSelf(new LandingState());
            container.BindInterfacesAndSelf(new RunToIdleState());
            container.BindInterfacesAndSelf(new DashState());
            container.BindInterfacesAndSelf(new WallSlideState());
            container.BindInterfacesAndSelf(new RollState());
            container.BindInterfacesAndSelf(new FrontFlipState());
            container.BindInterfacesAndSelf(new SmashState());
        }
    }
}