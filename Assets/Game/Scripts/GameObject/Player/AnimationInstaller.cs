using Gameplay;
using Modules.PlayerController;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class AnimationInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animations;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [FormerlySerializedAs("character")] [SerializeField] private CharacterController2D _character;

        public override void Install(DiContainer container)
        {
            Application.targetFrameRate = 140;
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer, receiver);

            container.BindSingle(_character);
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