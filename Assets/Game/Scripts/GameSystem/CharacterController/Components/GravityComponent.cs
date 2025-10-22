using UnityEngine;

public class GravityComponent
{
    private readonly PlayerController _controller;

    private const float radius = 0.1f;

    public bool IsGrounded { get; private set; }
    public Vector2 SurfaceNormal { get; private set; }
    public Transform CurrentGround { get; private set; }

    public float fallSpeed;
    private Vector3 lastGroundPos;
    private Vector3 gravityVector;

    public GravityComponent(PlayerController controller)
    {
        _controller = controller;
    }

    public Vector3 ApplyGravity(Vector3 position, Vector3 groundOffset)
    {
        if (TryGetGround(position, out RaycastHit2D hit))
        {
            SurfaceNormal = hit.normal;
            float angle = Vector2.Angle(hit.normal, Vector2.up);

            // Проверяем: стоим ли на земле и не двигаемся вверх от неё
            if (angle <= _controller.MaxSlopeAngle && Vector2.Dot(gravityVector, Vector2.down) >= 0f)
            {
                IsGrounded = true;
                CurrentGround = hit.collider.transform;
                lastGroundPos = CurrentGround.position;

                gravityVector = Vector3.zero;

                float verticalOffset = (hit.point.y + radius) - (position.y + groundOffset.y);
                return Vector3.up * verticalOffset;
            }
        }

        // В воздухе
        IsGrounded = false;
        CurrentGround = null;

        // Добавляем гравитацию вниз
        gravityVector += Vector3.down * _controller.Gravity * Time.deltaTime;

        return gravityVector * Time.deltaTime;
    }

    public Vector3 GetGroundOffset()
    {
        if (IsGrounded && CurrentGround != null)
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

        foreach (RaycastHit2D h in Physics2D.CircleCastAll(pos, radius, Vector2.down, 0.1f, _controller.GroundLayer))
        {
            if (h.point.y > maxY)
            {
                maxY = h.point.y;
                hit = h;
            }
        }

        return maxY > float.NegativeInfinity;
    }

    public void AddImpulse(float jumpForce, Vector3 direction)
    {
        IsGrounded = false;
        CurrentGround = null;

        // Прыжок по нормали поверхности
        gravityVector = direction.normalized * jumpForce;

        // Так как fallSpeed используется только по Y, перестаём его использовать
        fallSpeed = -gravityVector.y;

        SurfaceNormal = Vector3.up;
    }

    public void AddSlopeGravity(Vector3 slideSpeedAccum)
    {
        gravityVector += slideSpeedAccum;
    }
}