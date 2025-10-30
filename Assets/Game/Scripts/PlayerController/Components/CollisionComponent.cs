using Game.Scripts.PlayerController;
using Gameplay;
using UnityEngine;

public class CollisionComponent : ITickable
{
    private readonly PlayerController _player;

    public Vector2 SurfaceNormal { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsCeilingHit { get; private set; }
    public bool IsOnWall { get; private set; }
    public int WallDirection { get; private set; }
    public float DistanceToGround { get; private set; }

    public CollisionComponent(PlayerController player)
    {
        _player = player;
        Physics2D.queriesStartInColliders = false;
    }

    public void Tick()
    {
        CheckGround();
        CheckCeiling();
        CheckWall();
    }

    private void CheckGround()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            _player.Collider.bounds.center,
            Vector2.down,
            2,
            _player.Stats.LayerMask
        );

        IsGrounded = hit.collider != null;
        
        if (hit.collider != null)
        {
            SurfaceNormal = hit.normal;
            DistanceToGround = hit.distance;
        }
        else
        {
            SurfaceNormal = Vector2.zero;
            DistanceToGround = 9999f;
        }
    }

    private void CheckCeiling()
    {
        if (_player.Velocity.y <= 0)
        {
            IsCeilingHit = false;
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            _player.Collider.bounds.center,
            Vector2.up,
            0.2f,
            _player.Stats.LayerMask
        );

        IsCeilingHit = hit.collider != null;
    }

    private void CheckWall()
    {
        RaycastHit2D leftHit = Physics2D.Raycast(
            _player.Collider.bounds.center,
            Vector2.left,
            0.7f,
            _player.Stats.LayerMask
        );

        RaycastHit2D rightHit = Physics2D.Raycast(
            _player.Collider.bounds.center,
            Vector2.right,
            0.7f,
            _player.Stats.LayerMask
        );

        if (leftHit.collider != null && IsWall(leftHit))
        {
            IsOnWall = true;
            WallDirection = -1;
        }
        else if (rightHit.collider != null && IsWall(rightHit))
        {
            IsOnWall = true;
            WallDirection = 1;
        }
        else
        {
            IsOnWall = false;
            WallDirection = 0;
        }
    }

    private bool IsWall(RaycastHit2D hit)
    {
        float angle = Vector2.Angle(Vector2.up, hit.normal);
        return Mathf.Abs(angle - 90f) < 10f;
    }
}