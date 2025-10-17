using UnityEngine;

namespace Gameplay
{
    public class AnimatonInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animation;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animation, _spriteRenderer, receiver);
            container.BindSingle(spriteAnimator);
            container.BindSingle(receiver);
            
            container.Bind<IState>(new IdleState(spriteAnimator));
            container.Bind<IState>(new RunState(spriteAnimator));
            container.Bind<IState>(new FallState(spriteAnimator));


            var stateMachine = new StateMachine();
            container.BindSingle(stateMachine);
            container.BindSingle(new StateMachineController());

            var attackState = new AttackState();
            var pushAbilitySideState = new PushAbilitySideState();
            var pushAbilityUpState = new PushAbilityUPState();

            container.Bind<IState>(attackState);
            container.BindSingle(attackState);
            
            container.Bind<IState>(pushAbilitySideState);
            container.BindSingle(pushAbilitySideState);
            
            container.Bind<IState>(pushAbilityUpState);
            container.BindSingle(pushAbilityUpState);
            
        }
    }
}