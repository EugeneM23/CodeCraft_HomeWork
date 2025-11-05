using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class IdleState : BaseState
    {

        public override void Tick()
        {
            base.Tick();
            
            _animator.Play(AnimationID.Idle);

            if (Mathf.Abs(_character.Velocity.x) > 0.1f)
                AnimationFsm.SetState<RunState>();
        }
    }
}