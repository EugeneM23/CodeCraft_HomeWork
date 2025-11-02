using Gameplay;
using Modules.PlayerController;

namespace Game.Scripts.GameObject.Player
{
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
                container.BindInterfacesAndSelf(spriteAnimator);
                container.BindSingle(receiver);


                // --- State machine ---
                var stateMachine = new StateMachine();
                container.BindInterfacesAndSelf(stateMachine);

                // --- Animation states ---
                container.BindInterfacesAndSelf(new IdleState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new RunState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new RiseState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new FallMidState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new LandingState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new RunToIdleState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new DashState(spriteAnimator, stateMachine, _player));
                container.BindInterfacesAndSelf(new WallSlideState(spriteAnimator, stateMachine, _player));
            }
        }
    }
}