using System;
using System.Collections.Generic;
using System.IO;
using Game.Scripts.Modules.PlayerController.Data;
using UnityEngine;

namespace Modules.PlayerController
{
    public class ServiceLocator
    {
        private const string SETING_PATH = "Game/Scripts/Modules/PlayerController/Configs/MoveConfig.json";
        private readonly Dictionary<Type, object> _services = new();
        private readonly CharacterController2D _character;

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

        public ServiceLocator(CharacterController2D character)
        {
            _character = character;
            Initialize();
        }

        public void Initialize()
        {
            var impulseComponent = new ImpulseComponent(_character);
            Register(impulseComponent);

            Register(new CollisionComponent(_character));
            Register(new GravityComponent(_character));
            Register(new MoveComponent(_character, impulseComponent));
            Register(new WallSlidingComponent(_character));
            Register(new JumpComponent(_character));
            Register(new StairsMoveComponent(_character));
            Register(new MovingPlatformComponent(_character));
            Register(new SpriteFlipComponent(_character));

            PlayerStats stats = ConfigReader.Rread(Path.Combine(Application.dataPath, _character.SettingPath));
            Register(stats);
        }
    }
}