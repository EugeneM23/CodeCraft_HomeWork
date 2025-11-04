using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class RollState : BaseState
{
    private readonly float _dashDuration = 0.5f;
    private float _dashTimer;


    public override void Enter()
    {
        _dashTimer = _dashDuration;
        _animator.PlayForce(AnimationID.Roll).CanBreak(false);
        _contoller.OnCollisionHit += TransitToFall;
    }

    public override void Exit() => _contoller.OnCollisionHit -= TransitToFall;

    private void TransitToFall() => _stateMachine.SetState<FallMidState>();

    public override void Tick()
    {
        if (_contoller.IsWallSliding)
        {
            _stateMachine.SetState<WallSlideState>();
            return;
        }

        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
            TransitToFall();
    }
}