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
        float speed = _speed.CalculateSpeed(input.x);
        return new Vector2(speed, 0);
    }
}