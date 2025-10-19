using UnityEngine;

public class SimpleBulletFall : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = 9.8f;
    public float jumpForce = 5f;
    public float radius = 0.1f;
    public LayerMask groundLayer;
    public float maxSlopeAngle = 45f;

    private float fallSpeed;
    private bool hasHitGround;
    private Vector2 inputDir;
    private Transform currentGround;
    private Vector3 lastGroundPos;
    private bool jumpRequest;
    private Vector2 surfaceNormal;

    void Update()
    {
        inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpRequest = true;

        Vector3 move = Vector3.zero;

        move += GroundMoveOffset(); // сначала смещение платформы
        move += GravityMove(); // гравитация + прыжок + вертикальная коррекция
        move += SlopeMove(); // движение по наклону

        transform.position += move;
        jumpRequest = false;
    }

    private Vector3 GravityMove()
    {
        RaycastHit2D hit;
        if (DetectGround(out hit, out surfaceNormal))
        {
            float angle = Vector2.Angle(surfaceNormal, Vector2.up);
            if (angle <= maxSlopeAngle)
            {
                if (jumpRequest)
                {
                    fallSpeed = -jumpForce;
                    hasHitGround = false;
                    currentGround = null;
                }
                else
                {
                    hasHitGround = true;
                    currentGround = hit.collider.transform;
                    lastGroundPos = currentGround.position;
                    fallSpeed = 0f;

                    float verticalFix = (hit.point.y + radius) - (transform.position.y + GroundMoveOffset().y);
                    return Vector3.up * verticalFix;
                }
            }
            else
            {
                hasHitGround = false;
                currentGround = null;
            }
        }
        else
        {
            hasHitGround = false;
            currentGround = null;
        }

        fallSpeed += gravity * Time.deltaTime;
        return Vector3.down * fallSpeed * Time.deltaTime;
    }

    private Vector3 SlopeMove()
    {
        if (!hasHitGround)
            return new Vector3(inputDir.x * moveSpeed * Time.deltaTime, 0f, 0f);

        Vector2 tangent = new Vector2(surfaceNormal.y, -surfaceNormal.x);
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

    private bool DetectGround(out RaycastHit2D topHit, out Vector2 normal)
    {
        topHit = default;
        normal = Vector2.up;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, radius, Vector2.down, 0.1f, groundLayer);
        float maxY = float.NegativeInfinity;

        foreach (var hit in hits)
        {
            if (hit.point.y > maxY)
            {
                maxY = hit.point.y;
                topHit = hit;
                normal = hit.normal;
            }
        }

        return maxY > float.NegativeInfinity;
    }
}