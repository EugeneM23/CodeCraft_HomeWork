using System;
using System.Collections;
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

        public void BindSingle<T>(T instance)
        {
            if (_services.ContainsKey(typeof(T)) || _parent?.Contains(typeof(T)) == true)
                throw new InvalidOperationException($"The type {typeof(T)} has already been installed as singleton.");

            _services[typeof(T)] = instance;
        }

        public void Bind<T>(T instances)
        {
            if (_services.ContainsKey(typeof(List<T>)))
            {
                List<T> service = (List<T>)_services[typeof(List<T>)];
                service.Add(instances);
            }
            else
            {
                List<T> list = new List<T>();
                list.Add(instances);
                _services.Add(typeof(List<T>), list);
            }
        }

        public T Get<T>() => (T)Get(typeof(T));

        private object Get(Type type)
        {
            // Прямой биндинг
            if (_services.TryGetValue(type, out var service))
                return service;

            // Проверяем, не запрашивается ли коллекция (List<T>, IEnumerable<T>, T[])
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();

                if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);

                    if (_services.TryGetValue(listType, out var listObj))
                        return listObj;
                }
            }

            // Если запрашивается массив
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var listType = typeof(List<>).MakeGenericType(elementType);

                if (_services.TryGetValue(listType, out var listObj))
                {
                    var list = (IList)listObj;
                    var array = Array.CreateInstance(elementType, list.Count);
                    list.CopyTo(array, 0);
                    return array;
                }
            }

            // Проверяем, не запрашивается одиночный объект, а у нас хранится коллекция
            var listType2 = typeof(List<>).MakeGenericType(type);
            if (_services.TryGetValue(listType2, out var listObj2))
            {
                var list = (IList)listObj2;
                if (list.Count > 0)
                    return list[0];
            }

            // Пробуем у родителя
            return _parent?.Get(type);
        }

        public IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();

        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var instance = GameObject.Instantiate(prefab, position, rotation);

            // Если на инстансе есть GameObjectContext, устанавливаем ему наш контейнер как parent
            if (instance.GetComponentInChildren<GameObjectContext>() is { } context)
            {
                context.SetParent(this);
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
            tickableManager?.RemoveServices(this);

            _services.Clear();
        }

        public bool Contains(Type type) => _services.ContainsKey(type);
        public bool Contains(object instance) => _services.Values.Contains(instance);
        public bool Contains<T>() => Contains(typeof(T));
    }
}