using UnityEngine;

public class SimpleBulletFall : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = 9.8f;
    public float radius = 0.1f;
    public LayerMask groundLayer;

    private float fallSpeed;
    private bool hasHitGround;
    private Vector2 inputDir;
    private Transform currentGround;
    private Vector3 lastGroundPos;

    void Update()
    {
        inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
        Vector3 move = Vector3.zero;

        move += GravityMove();
        move += SlopeMove();
        move += GroundMoveOffset();

        transform.position += move;
    }

    private Vector3 GravityMove()
    {
        if (hasHitGround)
        {
            if (!IsGrounded())
            {
                hasHitGround = false;
                currentGround = null;
                fallSpeed = 0f;
            }
            else
                return Vector3.zero;
        }

        fallSpeed += gravity * Time.deltaTime;
        float distance = fallSpeed * Time.deltaTime;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, radius, Vector2.down, distance, groundLayer);
        RaycastHit2D topHit = default;
        float maxY = float.NegativeInfinity;

        foreach (var hit in hits)
        {
            if (hit.point.y > maxY)
            {
                maxY = hit.point.y;
                topHit = hit;
            }
        }

        if (hits.Length > 0)
        {
            transform.position = new Vector3(transform.position.x, topHit.point.y + radius, transform.position.z);
            hasHitGround = true;
            fallSpeed = 0f;
            currentGround = topHit.collider.transform;
            lastGroundPos = currentGround.position;
            return Vector3.zero;
        }

        currentGround = null;
        return Vector3.down * distance;
    }

    private Vector3 SlopeMove()
    {
        if (!hasHitGround)
            return new Vector3(inputDir.x * moveSpeed * Time.deltaTime, 0f, 0f);

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, radius, Vector2.down, 0.05f, groundLayer);
        RaycastHit2D topHit = default;
        float maxY = float.NegativeInfinity;

        foreach (var hit in hits)
        {
            if (hit.point.y > maxY)
            {
                maxY = hit.point.y;
                topHit = hit;
            }
        }

        if (hits.Length == 0)
            return Vector3.zero;

        Vector2 normal = topHit.normal;
        Vector2 tangent = new Vector2(normal.y, -normal.x);

        return (Vector3)(tangent.normalized * inputDir.x * moveSpeed * Time.deltaTime);
    }

    private Vector3 GroundMoveOffset()
    {
        if (hasHitGround && currentGround != null)
        {
            Vector3 offset = currentGround.position - lastGroundPos;
            lastGroundPos = currentGround.position;
            return offset;
        }

        return Vector3.zero;
    }

    private bool IsGrounded()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, radius, Vector2.down, 0.02f, groundLayer);
        return hits.Length > 0;
    }
}