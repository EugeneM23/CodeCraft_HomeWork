using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class RunState : BaseState
    {
        public override void Tick()
        {
            base.Tick();
            _animator.Play(AnimationID.Run);

            /*if (Mathf.Abs(_character.Velocity.x) < 0.1f && _character.MoveDirection == Vector2.zero)
                _stateMachine.SetState<RunToIdleState>();*/
        }
    }
}