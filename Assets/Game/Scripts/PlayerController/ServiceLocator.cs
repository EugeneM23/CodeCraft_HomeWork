using System;
using System.Collections.Generic;
using System.IO;

using Game.Scripts.PlayerController.Data;
using UnityEngine;

namespace PlayerController
{
    public class ServiceLocator
    {
        private const string SETING_PATH = "Game/Scripts/PlayerController/Configs/MoveConfig.json";
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
            _services[typeof(T)] = service;
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
            Register(new ImpulseComponent(player));
            Register(new DashController(player));
            Register(new SmashController(player));
            Register(new JumpController(player));
            Register(new MoveController(player));

            Register(new CollisionComponent(player));
            Register(new GravityComponent(player));
            Register(new MoveComponent(player));
            Register(new WallSlidingComponent(player));
            // Register(new LedgeGrabComponent(player)); 
            Register(new JumpComponent(player));
            Register(new StairsMoveComponent(player));
            Register(new MovingPlatformComponent(player));
            Register(new SlopeSlideComponent(player));

            PlayerStats stats = ConfigReader.Rread(Path.Combine(Application.dataPath, SETING_PATH));
            Register(stats);
        }
    }
}