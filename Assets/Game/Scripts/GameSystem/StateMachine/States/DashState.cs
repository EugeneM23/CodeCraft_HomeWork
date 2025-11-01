using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class DashState : BaseState
{
    private readonly float _dashDuration = 0.5f;
    private float _dashTimer;

    public DashState(SpriteAnimator animator, StateMachine stateMachine, PlayerController player)
        : base(animator, stateMachine, player)
    {
    }

    public override void Enter()
    {
        _dashTimer = _dashDuration; // сбрасываем таймер
        _animator.Play(AnimationID.Dash);
        _player.OnCollisionHit += TransitToFall;
    }

    private void TransitToFall() => _stateMachine.SetState<FallState>();

    public override void Tick()
    {
        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
            TransitToFall();
    }

    public override void Exit() => _player.OnCollisionHit -= TransitToFall;
}