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
            // 1. Прямой биндинг
            if (TryGetDirect(type, out var service))
                return service;

            // 2. Коллекции: List<T> или IEnumerable<T>
            if (TryGetCollection(type, out service))
                return service;

            // 3. Массивы
            if (TryGetArray(type, out service))
                return service;

            // 4. Один объект из коллекции
            if (TryGetFromCollection(type, out service))
                return service;

            // 5. Родительский контейнер
            return _parent?.Get(type);
        }

        private bool TryGetDirect(Type type, out object service)
        {
            return _services.TryGetValue(type, out service);
        }

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
            InjectMethods(target);
            InjectFields(target);
            InjectProperties(target);
        }

        private void InjectMethods(object target)
        {
            var methods = target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                            BindingFlags.FlattenHierarchy)
                .Where(m => m.IsDefined(typeof(InjectAttribute), true));

            foreach (var method in methods)
                InjectMethod(method, target);
        }

        private void InjectFields(object target)
        {
            var fields = target.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                           BindingFlags.FlattenHierarchy)
                .Where(f => f.IsDefined(typeof(InjectAttribute), true));

            foreach (var field in fields)
                InjectMember(field.FieldType, value => field.SetValue(target, value), field.Name,
                    target.GetType().Name);
        }

        private void InjectProperties(object target)
        {
            var properties = target.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                               BindingFlags.FlattenHierarchy)
                .Where(p => p.IsDefined(typeof(InjectAttribute), true) && p.CanWrite);

            foreach (var prop in properties)
                InjectMember(prop.PropertyType, value => prop.SetValue(target, value), prop.Name,
                    target.GetType().Name);
        }

        private void InjectMember(Type memberType, Action<object> setValue, string memberName, string targetTypeName)
        {
            var service = Get(memberType);
            if (service != null)
                setValue(service);
            else
                Debug.LogError(
                    $"[DiContainer] Missing dependency: '{memberType.Name}' for '{memberName}' in '{targetTypeName}'");
        }

        private void InjectMethod(MethodInfo method, object target)
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

        public IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();

        public bool Contains(Type type) => _services.ContainsKey(type);
        public bool Contains(object instance) => _services.Values.Contains(instance);
        public bool Contains<T>() => Contains(typeof(T));
    }
}