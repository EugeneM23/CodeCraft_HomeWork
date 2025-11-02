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
            base.Tick();

            if (Mathf.Abs(_player.Velocity.x) < 0.1f && _player.MoveDirection == Vector2.zero)
                _stateMachine.SetState<RunToIdleState>();
        }
    }
}