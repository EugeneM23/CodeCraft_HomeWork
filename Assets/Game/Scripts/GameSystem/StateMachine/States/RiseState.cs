using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class RiseState : BaseState
    {
        public RiseState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.StartJump).Interrupt(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<FallState>();
        }
    }

    public class FallState : BaseState
    {
        public FallState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Tick()
        {
            _animator.Play(AnimationID.Fly);

            if (_player.IsGrounded)
            {
                Debug.Log("Fall");
                _stateMachine.SetState<LandingState>();
            }
        }
    }

    public class LandingState : BaseState
    {
        public LandingState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator,
            stateMachine, player)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.Land).Interrupt(false);
        }

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt) 
                _stateMachine.SetState<IdleState>();
        }
    }
}