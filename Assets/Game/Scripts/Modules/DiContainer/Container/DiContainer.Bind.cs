using System;
using System.Collections.Generic;

namespace Gameplay
{
    public partial class DiContainer
    {
        public void BindSingle<T>(T instance)
        {
            if (_services.ContainsKey(typeof(T)) || _parent?.Contains(typeof(T)) == true)
                throw new InvalidOperationException($"The type {typeof(T)} has already been installed as singleton.");

            _services[typeof(T)] = instance;
        }

        public void BindInterface<T>(T instance)
        {
            var listType = typeof(List<T>);

            if (_services.TryGetValue(listType, out var existing))
            {
                var list = (List<T>)existing;
                list.Add(instance);
            }
            else
            {
                var newList = new List<T> { instance };
                _services[listType] = newList;
            }
        }
    }
}