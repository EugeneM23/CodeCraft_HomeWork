using Gameplay.Controllers;
using UnityEngine;

namespace Gameplay
{
    public class CharacterAnimationInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animations;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            container.BindInterfacesAndSelf(new SpriteAnimator(_animations, _spriteRenderer));
            container.BindInterfacesAndSelf(new StateMachine());
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
            container.BindInterfacesAndSelf(new AttackState());
        }
    }
}