using Gameplay;
using UnityEngine;
using UnityEngine;

public class MoveComponent
{
    private readonly PlayerController _controller;

    public MoveComponent(PlayerController player)
    {
        _controller = player;
    }

    public Vector3 Move()
    {
        CapsuleCollider2D capsule = _controller.Collider;
        Vector2 direction = _controller.InputDir.normalized;
        float distance = _controller.MoveSpeed * Time.deltaTime;

        RaycastHit2D hit = Physics2D.CapsuleCast(capsule.transform.position, capsule.size, CapsuleDirection2D.Vertical,
            0f, direction, distance, _controller.GroundLayer);

        if (hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) > _controller.MaxSlopeAngle)
        {
            Debug.DrawRay(capsule.transform.position, direction * distance, Color.red);
            return Vector3.zero;
        }

        Debug.DrawRay(capsule.transform.position, direction * distance, Color.green);
        
        if (!_controller.IsGrounded)
            return new Vector3(direction.x * distance, 0f, 0f);
        
        Vector2 tangent = new Vector2(_controller.SurfaceNormal.y, -_controller.SurfaceNormal.x);
        return tangent.normalized * direction.x * distance;
    }
}