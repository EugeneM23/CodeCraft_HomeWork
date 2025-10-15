using System;
using System.Linq;
using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer : IDisposable
    {
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (object service in _services.Values)
                if (service is IDisposable disposable)
                    disposable.Dispose();

            var manager = Resolve<TickableManager>();
            manager?.RemoveServices(this);
            
            _services.Clear();
        }

        public bool Contains(Type type) => _services.ContainsKey(type);
        public bool Contains(object instance) => _services.Values.Contains(instance);
        public bool Contains<T>() => Contains((object)typeof(T));
    }
}