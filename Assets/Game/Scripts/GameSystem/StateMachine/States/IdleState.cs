using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class IdleState : BaseState
    {
        public IdleState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character) : base(animator,
            stateMachine, character)
        {
        }

        public override void Enter() => Character.OnJump += TransitToJump;

        public override void Exit() => Character.OnJump -= TransitToJump;

        private void TransitToJump() => _stateMachine.SetState<JumpStartState>();

        public override void Tick()
        {
            base.Tick();
            
            _animator.Play(AnimationID.Idle);

            if (Mathf.Abs(Character.Velocity.x) > 0.1f)
                _stateMachine.SetState<RunState>();
        }
    }
}