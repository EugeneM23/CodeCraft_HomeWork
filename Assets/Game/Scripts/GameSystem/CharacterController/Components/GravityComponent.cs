using UnityEngine;

public class GravityComponent
{
    private readonly float gravity;
    private readonly float maxSlopeAngle;
    private readonly LayerMask groundLayer;
    private const float radius = 0.1f;

    public bool HasHitGround { get; private set; }
    public Vector2 SurfaceNormal { get; private set; }
    public Transform CurrentGround { get; private set; }

    public float fallSpeed;
    private Vector3 lastGroundPos;

    public GravityComponent(LayerMask groundLayer, float gravity, float maxSlopeAngle)
    {
        this.groundLayer = groundLayer;
        this.gravity = gravity;
        this.maxSlopeAngle = maxSlopeAngle;
    }

    public Vector3 ApplyGravity(Vector3 position, Vector3 groundOffset)
    {
        if (TryGetGround(position, out RaycastHit2D hit))
        {
            SurfaceNormal = hit.normal;

            float angle = Vector2.Angle(hit.normal, Vector2.up);

            if (angle <= maxSlopeAngle && fallSpeed >= 0f)
            {
                HasHitGround = true;
                CurrentGround = hit.collider.transform;
                lastGroundPos = CurrentGround.position;
                fallSpeed = 0f;

                float verticalOffset = (hit.point.y + radius) - (position.y + groundOffset.y);
                return Vector3.up * verticalOffset; // приклеиваем к земле
            }
        }

        HasHitGround = false;
        CurrentGround = null;
        fallSpeed += gravity * Time.deltaTime;
        
        return Vector3.down * fallSpeed * Time.deltaTime;
    }

    public Vector3 GetGroundOffset()
    {
        if (HasHitGround && CurrentGround != null)
        {
            Vector3 offset = CurrentGround.position - lastGroundPos;
            lastGroundPos = CurrentGround.position;
            return offset;
        }

        return Vector3.zero;
    }

    private bool TryGetGround(Vector3 pos, out RaycastHit2D hit)
    {
        hit = default;
        float maxY = float.NegativeInfinity;

        foreach (RaycastHit2D h in Physics2D.CircleCastAll(pos, radius, Vector2.down, 0.1f, groundLayer))
        {
            if (h.point.y > maxY)
            {
                maxY = h.point.y;
                hit = h;
            }
        }

        return maxY > float.NegativeInfinity;
    }

    public void AddImpulse(float jumpForce)
    {
        fallSpeed = -jumpForce;
        HasHitGround = false;
        CurrentGround = null;
    }
}