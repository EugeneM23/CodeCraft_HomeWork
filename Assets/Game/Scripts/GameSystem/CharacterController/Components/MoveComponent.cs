using UnityEngine;

public class MoveComponent
{
    private float moveSpeed;

    public MoveComponent(float moveSpeed)
    {
        this.moveSpeed = moveSpeed;
    }

    public Vector3 Move(Vector2 input, bool grounded, Vector2 normal, Collider2D collider, LayerMask groundLayer)
    {
        RaycastHit2D hit = Physics2D.Raycast(collider.transform.position + new Vector3(0, 1, 0), input, 1, groundLayer);

        Vector2 hitNormal = hit.normal;

        float angle = Vector2.Angle(hitNormal, Vector2.up);

        if (hit.collider != null && angle >= 89)
        {
            Debug.DrawRay(collider.transform.position + new Vector3(0, 1, 0), input * 1, Color.red);
            return Vector3.zero;
        }

        Debug.DrawRay(collider.transform.position + new Vector3(0, 1, 0), input * 1, Color.red);


        if (!grounded) return new Vector3(input.x * moveSpeed * Time.deltaTime, 0f, 0f);

        Vector2 tangent = new Vector2(normal.y, -normal.x);
        return tangent.normalized * input.x * moveSpeed * Time.deltaTime;
    }
}