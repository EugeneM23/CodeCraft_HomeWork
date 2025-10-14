using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public class DiContainer : IDisposable
    {
        public event Action<object> OnServiceDisposed;

        private readonly Dictionary<Type, object> _services = new();
        private readonly DiContainer _parent;
        private bool _disposed;

        public DiContainer(DiContainer parent = null) => _parent = parent;

        public void Install(Installer installer)
        {
            if (installer == null) return;

            installer.Install(this);

            foreach (var service in _services.Values)
                Inject(service);
        }

        public void Add<T>(T instance)
        {
            _services[typeof(T)] = instance;
        }

        public T Get<T>() => (T)Get(typeof(T));

        public object Get(Type type)
        {
            // Сначала ищем локально, потом у родителя (если есть)
            return _services.TryGetValue(type, out var service) ? service : _parent?.Get(type);
        }

        public IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();

        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var instance = GameObject.Instantiate(prefab, position, rotation);

            // Если на инстансе есть GameObjectContext, устанавливаем ему наш контейнер как parent
            if (instance.GetComponentInChildren<GameObjectContext>() is { } context)
            {
                context.Initialize(this);
                Get<TickableManager>().RunAndInitialize(context.Container);
            }

            return instance;
        }

        public void Inject(object target)
        {
            if (target == null) return;

            var methods = target.GetType()
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.FlattenHierarchy)
                .Where(m => m.IsDefined(typeof(InjectAttribute), true));

            foreach (var method in methods)
                TryInvokeInjectMethod(method, target);
        }

        private void TryInvokeInjectMethod(MethodInfo method, object target)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            var hasMissing = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                var service = Get(type);
                if (service == null)
                {
                    hasMissing = true;
                    Debug.LogError(
                        $"[DiContainer] Missing dependency: '{type.Name}' for '{target.GetType().Name}.{method.Name}'");
                }

                args[i] = service;
            }

            if (!hasMissing)
                method.Invoke(target, args);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            foreach (object service in _services.Values)
            {
                if (service is IDisposable disposable)
                    disposable.Dispose();
            }

            TickableManager tickableManager = Get<TickableManager>();
            tickableManager.RemoveServices(this);

            _services.Clear();
        }

        public bool Contains(object instance) => _services.Values.Contains(instance);
    }
}