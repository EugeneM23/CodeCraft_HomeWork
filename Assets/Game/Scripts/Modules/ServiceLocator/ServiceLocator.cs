using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gameplay
{
    public static class ServiceLocator
    {
        public static int Count => _services.Count;
        private static readonly Dictionary<string, object> _services = new();

        public static void Add(Enum id, object service)
        {
            _services[id.ToString()] = service;
        }

        public static TContract Get<TContract>(Enum id)
        {
            return (TContract)_services[id.ToString()];
        }

        public static object Get(Type type)
        {
            foreach (var (key, value) in _services)
                if (value.GetType() == type)
                    return value;

            return default;
        }

        public static IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();
        public static object[] GetAll() => _services.Values.ToArray();
    }
}