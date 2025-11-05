using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class FallMidState : BaseState
    {
        public override void Tick()
        {
            base.Tick();
            _animator.Play(AnimationID.AirMid);
            if (_character.IsGrounded) AnimationFsm.SetState<LandingState>();
        }
    }
}