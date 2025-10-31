using System;
using System.Collections.Generic;
using System.IO;
using Game.Scripts.Modules.PlayerController.Components;
using Game.Scripts.Modules.PlayerController.Controllers;
using Game.Scripts.Modules.PlayerController.Data;
using Game.Scripts.Modules.PlayerController.Enviroment;
using UnityEngine;

namespace Game.Scripts.Modules.PlayerController
{
    public class ServiceLocator
    {
        private const string SETING_PATH = "Game/Scripts/Modules/PlayerController/Configs/MoveConfig.json";
        private readonly Dictionary<Type, object> _services = new();
        private readonly PlayerController _player;

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
            _player = player;
            Initialize();
        }

        public void Initialize()
        {
            Register(new ImpulseComponent(_player));
            Register(new DashController(_player));
            Register(new SmashController(_player));
            Register(new JumpController(_player));
            Register(new MoveController(_player));

            Register(new CollisionComponent(_player));
            Register(new GravityComponent(_player));
            Register(new MoveComponent(_player));
            Register(new WallSlidingComponent(_player));
            Register(new JumpComponent(_player));
            Register(new StairsMoveComponent(_player));
            Register(new MovingPlatformComponent(_player));

            //Register(new LedgeGrabComponent(player)); 
            //Register(new SlopeSlideComponent(player));

            PlayerStats stats = ConfigReader.Rread(Path.Combine(Application.dataPath, SETING_PATH));
            Register(stats);
        }
    }
}