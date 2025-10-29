using Game.Scripts.PlayerController;
using UnityEngine;

public class CollisionComponent : ITickable
{
    private readonly PlayerController _player;

    public Vector2 SurfaceNormal { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsCeilingHit { get; private set; }

    public CollisionComponent(PlayerController player)
    {
        _player = player;

        Physics2D.queriesStartInColliders = false;
    }

    public void Tick()
    {
        IsCeilingHit = CheckCeilingCollision();
        IsGrounded = CheckGroundCollision();
        UpdateSurfaceNormal();
    }

    private bool CheckGroundCollision()
    {
        bool groundHit = Physics2D.CapsuleCast(
            _player.Collider.bounds.center,
            _player.Collider.size,
            _player.Collider.direction,
            0,
            Vector2.down,
            0.1f,
            _player.Stats.PlayerLayer
        );

        return groundHit;
    }

    private bool CheckCeilingCollision()
    {
        bool ceilingHit = Physics2D.CapsuleCast(
            _player.Collider.bounds.center,
            _player.Collider.size,
            _player.Collider.direction,
            0,
            Vector2.up,
            _player.Stats.GrounderDistance,
            _player.Stats.PlayerLayer
        );

        if (ceilingHit && _player.Velocity.y > 0)
            return true;

        return false;
    }

    private void UpdateSurfaceNormal()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            _player.Collider.bounds.center,
            Vector2.down,
            _player.Stats.GrounderDistance,
            _player.Stats.PlayerLayer
        );

        if (hit)
            SurfaceNormal = hit.normal;
        else
            SurfaceNormal = Vector2.zero;
    }
}