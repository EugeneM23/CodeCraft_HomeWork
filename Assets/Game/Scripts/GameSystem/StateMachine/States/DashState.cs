using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class DashState : BaseState
{
    private readonly float _dashDuration = 0.5f;
    private float _dashTimer;

    public DashState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character)
        : base(animator, stateMachine, character)
    {
    }

    public override void Enter()
    {
        _dashTimer = _dashDuration;
        _animator.PlayForce(AnimationID.Dash).Interrupt(false);
        Character.OnCollisionHit += TransitToFall;
    }

    public override void Exit() => Character.OnCollisionHit -= TransitToFall;

    private void TransitToFall() => _stateMachine.SetState<FallMidState>();

    public override void Tick()
    {
        if (Character.IsWallSliding)
        {
            _stateMachine.SetState<WallSlideState>();
            return;
        }

        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
            TransitToFall();
    }
}

public class RollState : BaseState
{
    private readonly float _dashDuration = 0.5f;
    private float _dashTimer;

    public RollState(SpriteAnimator animator, StateMachine stateMachine, CharacterController2D character)
        : base(animator, stateMachine, character)
    {
    }

    public override void Enter()
    {
        _dashTimer = _dashDuration;
        _animator.PlayForce(AnimationID.Roll).Interrupt(false);
        Character.OnCollisionHit += TransitToFall;
    }

    public override void Exit() => Character.OnCollisionHit -= TransitToFall;

    private void TransitToFall() => _stateMachine.SetState<FallMidState>();

    public override void Tick()
    {
        if (Character.IsWallSliding)
        {
            _stateMachine.SetState<WallSlideState>();
            return;
        }

        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
            TransitToFall();
    }
}