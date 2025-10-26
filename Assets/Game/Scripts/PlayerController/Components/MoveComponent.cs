using Game.Scripts.PlayerController;
using UnityEngine;

internal class MoveComponent : IMoveComponent
{
    private readonly CollisionComponent _collision;
    private readonly SpeedComponent _speed;
    private float _lastInputX;

    public MoveComponent(CollisionComponent collision, SpeedComponent speed)
    {
        _collision = collision;
        _speed = speed;
    }

    public Vector2 Move(Vector2 input)
    {
        // сохраняем последний инпут, даже если 0
        _lastInputX = input.x;

        if (_collision.IsGrounded)
            return MoveOnGround();

        return MoveInAir();
    }

    private Vector2 MoveInAir()
    {
        float speed = _speed.CalculateSpeed(_lastInputX, false);
        return new Vector2(Mathf.Sign(_lastInputX != 0 ? _lastInputX : speed) * Mathf.Abs(speed), 0);
    }

    private Vector2 MoveOnGround()
    {
        Vector2 normal = _collision.SurfaceNormal == Vector2.zero ? Vector2.up : _collision.SurfaceNormal;
        Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

        float speed = _speed.CalculateSpeed(_lastInputX, true);
        return tangent * speed;
    }
}