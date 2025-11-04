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

            if (Mathf.Abs(_contoller.Velocity.x) < 0.1f && _contoller.MoveDirection.x == 0)
                _stateMachine.SetState<RunToIdleState>();
        }
    }
}