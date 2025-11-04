using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class BaseState
    {
        protected SpriteAnimator _animator;
        protected StateMachine _stateMachine;
        protected CharacterController2D _contoller;
        protected Character _character;

        [Inject]
        public void Construct(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D controller,
            Character player)
        {
            _character = player;
            _animator = animator;
            _stateMachine = stateMachine;
            _contoller = controller;
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