using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class BaseState
    {
        protected SpriteAnimator _animator;
        protected StateMachine _stateMachine;
        protected CharacterController2D _character;

        [Inject]
        public void Construct(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D сharacter)
        {
            _animator = animator;
            _stateMachine = stateMachine;
            _character = сharacter;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public virtual void Tick()
        {
        }
    }
}