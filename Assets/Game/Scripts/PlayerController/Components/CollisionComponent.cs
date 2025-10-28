using Game.Scripts.PlayerController;
using UnityEngine;

public class CollisionComponent
{
    private readonly CapsuleCollider2D _collider;
    private readonly ScriptableStats _stats;
    private readonly PlayerController _player;

    public Vector2 SurfaceNormal { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsCeilingHit { get; private set; }

    public CollisionComponent(CapsuleCollider2D collider, ScriptableStats stats, PlayerController player)
    {
        _collider = collider;
        _stats = stats;
        _player = player;

        Physics2D.queriesStartInColliders = false;
    }

    public void DetectCollisions()
    {
        IsCeilingHit = CheckCeilingCollision();
        IsGrounded = CheckGroundCollision();

        // Обновляем нормаль поверхности
        UpdateSurfaceNormal();
    }

    private bool CheckGroundCollision()
    {
        bool groundHit = Physics2D.CapsuleCast(
            _collider.bounds.center,
            _collider.size,
            _collider.direction,
            0,
            Vector2.down,
            _stats.GrounderDistance,
            _stats.PlayerLayer
        );

        return groundHit;
    }

    private bool CheckCeilingCollision()
    {
        bool ceilingHit = Physics2D.CapsuleCast(
            _collider.bounds.center,
            _collider.size,
            _collider.direction,
            0,
            Vector2.up,
            _stats.GrounderDistance,
            _stats.PlayerLayer
        );

        if (ceilingHit && _player._frameVelocity.y > 0)
            return true;

        return false;
    }

   
    private void UpdateSurfaceNormal()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            _collider.bounds.center,
            Vector2.down,
            _stats.GrounderDistance + 2f, // чуть больше для надёжности
            _stats.PlayerLayer
        );

        if (hit)
            SurfaceNormal = hit.normal;
        else
            SurfaceNormal = Vector2.zero;

        Debug.DrawRay(_collider.bounds.center, Vector2.down * (_stats.GrounderDistance + 2f), Color.green);
        if (hit) Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.red);
    }
}
