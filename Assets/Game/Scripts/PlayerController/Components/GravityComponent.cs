using Game.Scripts.PlayerController;
using UnityEngine;

public class GravityComponent : IVelocity
{
    private readonly PlayerController _player;

    public GravityComponent(PlayerController player)
    {
        _player = player;
    }

    public Vector2 GetVelocity()
    {
        if (_player.IsOnStairs)
            return Vector2.zero;

        if (_player.IsCeilingHit)
            return new Vector2(0, -5);

        float gravity = _player.Stats.FallAcceleration;
        float currentYVelocity = _player.Velocity.y;

        float newYVelocity =
            Mathf.MoveTowards(currentYVelocity, -_player.Stats.MaxFallSpeed, gravity * Time.fixedDeltaTime);

        return new Vector2(0, newYVelocity);
    }
}