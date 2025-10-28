using System;
using System.Collections.Generic;
using Game.Scripts.PlayerController;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;

public class ServiceLocator
{
    private readonly Dictionary<Type, object> _services = new();

    public T Get<T>()
    {
        var type = typeof(T);
        if (_services.TryGetValue(type, out var service))
            return (T)service;

        throw new Exception($"Service of type {type} not found in ServiceLocator!");
    }

    public void Register<T>(T service)
    {
        var type = typeof(T);
        _services[type] = service;
    }

    public List<T> GetAll<T>()
    {
        var list = new List<T>();

        foreach (var service in _services.Values)
            if (service is T match)
                list.Add(match);

        return list;
    }

    public ServiceLocator(PlayerController player)
    {
        var collision = new CollisionComponent(player);
        var gravity = new GravityComponent(player);
        var move = new MoveComponent(player);
        var wallSlide = new WallSlidingComponent(player);
        var ledgeGrab = new LedgeGrabComponent(player);
        var jump = new JumpComponent(player);
        var stairs = new StairsMoveComponent(player);
        var movingPlatform = new MovingPlatformComponent(player);
        var slopeSlide = new SlopeSlideComponent(player);
        var moveController = new MoveController(player);
        var jumpController = new JumpController(player);

        Register(jumpController);
        Register(moveController);
        Register(collision);
        Register(gravity);
        Register(move);
        Register(wallSlide);
        //Register(ledgeGrab);
        Register(jump);
        Register(stairs);
        Register(movingPlatform);
        Register(slopeSlide);
    }
}