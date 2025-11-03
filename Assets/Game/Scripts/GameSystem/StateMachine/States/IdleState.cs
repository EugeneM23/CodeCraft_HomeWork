using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class IdleState : BaseState
    {
        public override void Enter() => _character.OnJump += TransitToJump;

        public override void Exit() => _character.OnJump -= TransitToJump;

        private void TransitToJump() => _stateMachine.SetState<JumpStartState>();

        public override void Tick()
        {
            base.Tick();
            
            _animator.Play(AnimationID.Idle);

            if (Mathf.Abs(_character.Velocity.x) > 0.1f)
                _stateMachine.SetState<RunState>();
        }
    }
}