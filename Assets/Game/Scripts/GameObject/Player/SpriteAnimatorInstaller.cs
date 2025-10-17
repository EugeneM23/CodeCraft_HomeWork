using Game.Scripts.GameSystem.Controllers.Player.StateMachine;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public class SpriteAnimatorInstaller : Installer
    {
        [SerializeField] private SpriteAnimation[] _animation;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            var spriteAnimator = new SpriteAnimator(_animation, _spriteRenderer);
            container.BindSingle(spriteAnimator);
            container.BindSingle(new AnimationController());


            var stateMachine = new StateMachine();
            container.BindSingle(stateMachine);
            container.BindSingle(new StateMachineController());

            container.Bind<IState>(new IdleState(stateMachine));
            container.Bind<IState>(new RunState(stateMachine));
            container.Bind<IState>(new FallState(stateMachine));
            container.Bind<IState>(new AttackState(stateMachine, spriteAnimator));
        }
    }
}