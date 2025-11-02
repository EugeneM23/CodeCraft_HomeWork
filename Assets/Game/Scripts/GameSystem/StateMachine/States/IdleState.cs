using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class IdleState : BaseState
    {
        public IdleState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Enter() => _player.OnJump += TransitToJump;

        public override void Exit() => _player.OnJump -= TransitToJump;

        private void TransitToJump() => _stateMachine.SetState<JumpStartState>();

        public override void Tick()
        {
            base.Tick();
            
            _animator.Play(AnimationID.Idle);

            if (Mathf.Abs(_player.Velocity.x) > 0.1f)
                _stateMachine.SetState<RunState>();
        }
    }
}