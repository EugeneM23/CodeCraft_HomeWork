using System;
using System.Collections.Generic;
using System.Linq;

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

        public static IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();
    }
}