using Gameplay;
using UnityEngine;
using UnityEngine;
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
        move += HorizontalMove();
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
            {
                return Vector3.zero;
            }
        }

        fallSpeed += gravity * Time.deltaTime;
        float distance = fallSpeed * Time.deltaTime;

        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, Vector2.down, distance, groundLayer);

        if (hit.collider != null)
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + radius, transform.position.z);
            hasHitGround = true;
            fallSpeed = 0f;
            currentGround = hit.collider.transform;
            lastGroundPos = currentGround.position;
            return Vector3.zero;
        }

        currentGround = null;
        return Vector3.down * distance;
    }

    private Vector3 HorizontalMove()
    {
        return new Vector3(inputDir.x * moveSpeed * Time.deltaTime, 0f, 0f);
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
        float checkDistance = 0.02f;
        return Physics2D.CircleCast(transform.position, radius, Vector2.down, checkDistance, groundLayer);
    }
}