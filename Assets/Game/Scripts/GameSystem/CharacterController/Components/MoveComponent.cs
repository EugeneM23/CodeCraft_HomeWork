using UnityEngine;

public class MoveComponent 
{
    private float moveSpeed;

    public MoveComponent(float moveSpeed)
    {
        this.moveSpeed = moveSpeed;
    }

    public Vector3 Move(Vector2 input, bool grounded, Vector2 normal)
    {
        if (!grounded) return new Vector3(input.x * moveSpeed * Time.deltaTime, 0f, 0f);

        Vector2 tangent = new Vector2(normal.y, -normal.x);
        return tangent.normalized * input.x * moveSpeed * Time.deltaTime;
    }
}