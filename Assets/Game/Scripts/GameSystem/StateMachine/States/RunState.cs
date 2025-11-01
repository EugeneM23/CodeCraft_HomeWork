using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class RunState : BaseState
    {
        public RunState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(animator,
            stateMachine, player)
        {
        }

        public override void Enter()
        {
            _animator.Play(AnimationID.Run);
        }

        public override void Tick()
        {
            if (!_player.IsGrounded)
            {
                _stateMachine.SetState<FallState>();
                return;
            }

            if (Mathf.Abs(_player.Velocity.x) < 0.1f) 
                _stateMachine.SetState<RunToIdleState>();
        }
    }

    public class RunToIdleState : BaseState
    {
        public RunToIdleState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player) : base(
            animator,
            stateMachine, player)
        {
        }

        public override void Enter() => _animator.Play(AnimationID.RunToIdle).Interrupt(false);

        public override void Tick()
        {
            if (_animator.CurrentAnimation.CanInterrupt)
                _stateMachine.SetState<IdleState>();
        }
    }
}