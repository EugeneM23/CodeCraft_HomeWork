using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class FallMidState : BaseState
    {
        public FallMidState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character) : base(
            animator,
            stateMachine, character)
        {
        }

        public override void Tick()
        {
            base.Tick();
            _animator.Play(AnimationID.AirMid);
            if (Character.IsGrounded) _stateMachine.SetState<LandingState>();
        }
    }
}