using Game.Scripts.PlayerController;
using UnityEngine;

public class CollisionComponent : ITickable
{
    private readonly PlayerController _player;

    public Vector2 SurfaceNormal { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsCeilingHit { get; private set; }
    public bool IsOnWall { get; private set; }
    public Vector2 WallNormal { get; private set; }
    public int WallDirection { get; private set; } 

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
        UpdateWallCollision();
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

    private void UpdateWallCollision()
    {
        RaycastHit2D leftHit = ScanWall(Vector2.left);
        RaycastHit2D rightHit = ScanWall(Vector2.right);

        if (leftHit.collider != null && IsWallAngle(leftHit))
        {
            WallNormal = leftHit.normal;
            IsOnWall = true;
            WallDirection = -1;
        }
        else if (rightHit.collider != null && IsWallAngle(rightHit))
        {
            WallNormal = rightHit.normal;
            IsOnWall = true;
            WallDirection = 1;
        }
        else
        {
            IsOnWall = false;
            WallNormal = Vector2.zero;
            WallDirection = 0;
        }
    }

    private RaycastHit2D ScanWall(Vector2 direction)
    {
        Vector2 origin = _player.Collider.bounds.center;
        float length = 0.7f;

        Debug.DrawLine(origin, origin + direction * length, Color.cyan);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, length, _player.Stats.PlayerLayer);

        if (hit.collider == _player.Collider)
            return default;

        return hit;
    }

    private bool IsWallAngle(RaycastHit2D hit)
    {
        float angle = Vector2.Angle(Vector2.up, hit.normal);
        return Mathf.Abs(angle - 90f) < 10f;
    }
}