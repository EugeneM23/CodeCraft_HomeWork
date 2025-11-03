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
        _animator.PlayForce(AnimationID.Roll).Interrupt(false);
        _character.OnCollisionHit += TransitToFall;
    }

    public override void Exit() => _character.OnCollisionHit -= TransitToFall;

    private void TransitToFall() => _stateMachine.SetState<FallMidState>();

    public override void Tick()
    {
        if (_character.IsWallSliding)
        {
            _stateMachine.SetState<WallSlideState>();
            return;
        }

        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
            TransitToFall();
    }
}