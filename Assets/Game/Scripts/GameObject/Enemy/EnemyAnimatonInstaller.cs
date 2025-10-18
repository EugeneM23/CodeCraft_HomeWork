using UnityEngine;

namespace Gameplay
{
    public class EnemyAnimationInstaller : Installer
    {
        [Header("Animation Settings")] [SerializeField]
        private SpriteAnimation[] _animations;

        [SerializeField] private SpriteRenderer _spriteRenderer;

        public override void Install(DiContainer container)
        {
            // --- Core animation setup ---
            var receiver = new AnimationEventReceiver();
            var spriteAnimator = new SpriteAnimator(_animations, _spriteRenderer, receiver);

            container.BindSingle(spriteAnimator);
            container.BindSingle(receiver);

            // --- Animation states ---
            container.BindInterface<IState>(new IdleState(spriteAnimator));
            container.BindInterface<IState>(new RunState(spriteAnimator));
            container.BindInterface<IState>(new FallState(spriteAnimator));

            // --- State machine ---
            var stateMachine = new StateMachine();
            container.BindSingle(stateMachine);
            container.BindSingle(new EnemyStateMachineController());

            // --- Ability-related states ---
            var attackState = new AttackState();
            var pushAbilitySideState = new PushAbilitySideState();
            var pushAbilityUpState = new PushAbilityUPState();

            container.BindInterface<IState>(attackState);
            container.BindSingle(attackState);

            container.BindInterface<IState>(pushAbilitySideState);
            container.BindSingle(pushAbilitySideState);

            container.BindInterface<IState>(pushAbilityUpState);
            container.BindSingle(pushAbilityUpState);
        }
    }
}