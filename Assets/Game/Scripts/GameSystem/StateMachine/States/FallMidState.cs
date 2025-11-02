using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class FallMidState : BaseState
    {
        public FallMidState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator,
            stateMachine, player)
        {
        }

        public override void Tick()
        {
            base.Tick();
            _animator.Play(AnimationID.AirMid);
            if (_player.IsGrounded) _stateMachine.SetState<LandingState>();
        }
    }
}