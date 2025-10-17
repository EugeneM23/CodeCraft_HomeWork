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

            var stateMachine = new StateMachine();
            container.BindSingle(stateMachine);
            container.BindSingle(new StateMachineController());

            container.Bind<IState>(new IdleState(spriteAnimator));
            container.Bind<IState>(new RunState(spriteAnimator));
            container.Bind<IState>(new FallState(spriteAnimator));

            var attackState = new AttackState();

            container.Bind<IState>(attackState);
            container.BindSingle(attackState);
        }
    }
}