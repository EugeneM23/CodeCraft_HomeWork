using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class RunState : BaseState
    {
        public RunState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character) : base(animator,
            stateMachine, character)
        {
        }


        public override void Tick()
        {
            base.Tick();
            _animator.Play(AnimationID.Run);

            if (Mathf.Abs(Character.Velocity.x) < 0.1f && Character.MoveDirection == Vector2.zero)
                _stateMachine.SetState<RunToIdleState>();
        }
    }
}