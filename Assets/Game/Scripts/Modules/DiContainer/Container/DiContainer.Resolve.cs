using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Gameplay
{
    public partial class DiContainer
    {
        public T Resolve<T>() => (T)Resolve(typeof(T));

        private object Resolve(Type type)
        {
            if (TryGetDirect(type, out var service)) return service;
            if (TryGetCollection(type, out service)) return service;
            if (TryGetArray(type, out service)) return service;
            if (TryGetFromCollection(type, out service)) return service;

            return _parent?.Resolve(type);
        }

        private bool TryGetDirect(Type type, out object service) => _services.TryGetValue(type, out service);

        private bool TryGetCollection(Type type, out object service)
        {
            service = null;
            if (!type.IsGenericType) return false;

            var elementType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);

            if (!_services.TryGetValue(listType, out var listObj)) return false;

            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>))
            {
                service = listObj;
                return true;
            }

            return false;
        }

        private bool TryGetArray(Type type, out object service)
        {
            service = null;
            if (!type.IsArray) return false;

            var elementType = type.GetElementType();
            var listType = typeof(List<>).MakeGenericType(elementType);
            if (!_services.TryGetValue(listType, out var listObj)) return false;

            var list = (IList)listObj;
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            service = array;
            return true;
        }

        private bool TryGetFromCollection(Type type, out object service)
        {
            service = null;
            var listType = typeof(List<>).MakeGenericType(type);
            if (!_services.TryGetValue(listType, out var listObj)) return false;

            var list = (IList)listObj;
            if (list.Count > 0)
            {
                service = list[0];
                return true;
            }

            return false;
        }

        public IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();
    }
}