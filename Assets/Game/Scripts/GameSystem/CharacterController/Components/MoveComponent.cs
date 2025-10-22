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
        RaycastHit2D hit = Physics2D.Raycast(_controller.Collider.transform.position + new Vector3(0, 1, 0),
            _controller.InputDir,
            1, _controller.GroundLayer);

        Vector2 hitNormal = hit.normal;

        float angle = Vector2.Angle(hitNormal, Vector2.up);

        if (hit.collider != null && angle >= 55)
        {
            Debug.DrawRay(_controller.Collider.transform.position + new Vector3(0, 1, 0), _controller.InputDir * 1,
                Color.red);
            return Vector3.zero;
        }

        Debug.DrawRay(_controller.Collider.transform.position + new Vector3(0, 1, 0), _controller.InputDir * 1,
            Color.red);


        if (!_controller.IsGrounded)
            return new Vector3(_controller.InputDir.x * _controller.MoveSpeed * Time.deltaTime, 0f, 0f);

        Vector2 tangent = new Vector2(_controller.SurfaceNormal.y, -_controller.SurfaceNormal.x);
        return tangent.normalized * _controller.InputDir.x * _controller.MoveSpeed * Time.deltaTime;
    }
}