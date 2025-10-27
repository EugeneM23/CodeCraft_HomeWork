using Game.Scripts.PlayerController;
using Gameplay;
using SpriteShadersUltimate.Demo;
using UnityEngine;

internal class MoveComponent : IMoveComponent
{
    private readonly CollisionComponent _collision;
    private readonly SpeedComponent _speed;
    private PlayerController _player;
    private float _lastInputX;

    public MoveComponent(CollisionComponent collision, SpeedComponent speed, PlayerController player)
    {
        _collision = collision;
        _speed = speed;
        _player = player;
    }

    public Vector2 Move(Vector2 input)
    {
        _lastInputX = input.x;

        if (_collision.IsGrounded)
            return MoveOnGround();

        return MoveInAir();
    }

    private Vector2 MoveInAir()
    {
        float speed = _speed.CalculateSpeed(_lastInputX, false);

        float direction = _lastInputX != 0 ? Mathf.Sign(_lastInputX) : Mathf.Sign(speed);
        float x = direction * Mathf.Abs(speed);

        return new Vector2(x, 0);
    }

    private Vector2 MoveOnGround()
    {
        Vector2 normal = _collision.SurfaceNormal;
        Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

        float speed = _speed.CalculateSpeed(_lastInputX, true);
        return tangent.Log() * speed;
    }
}