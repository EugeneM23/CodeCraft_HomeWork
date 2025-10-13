using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public class DiContainer
    {
        private readonly Dictionary<Type, object> _services = new();

        private Installer[] _installers;

        private readonly DiContainer _parent;

        public DiContainer(DiContainer parent)
        {
            _parent = parent;
        }

        public void Install(Installer installer)
        {
            installer.Install(this);

            foreach (var (key, value) in _services)
                Inject(value);
        }

        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            T component = GameObject.Instantiate(prefab, position, rotation);

            Inject(component);

            MonoBehaviour[] allComponentsInPrefab = component.GetComponentsInChildren<MonoBehaviour>();
            foreach (var comp in allComponentsInPrefab)
                Inject(comp);

            return component;
        }

        public void Add<T>(T service)
        {
            Type type = typeof(T);
            _services[type] = service;
        }

        public object Get(Type type)
        {
            if (_services.TryGetValue(type, out object service))
                return service;

            return null;
        }

        public IEnumerable<T> GetAll<T>()
        {
            return _services.Values.OfType<T>();
        }

        public void Inject(object target)
        {
            Type type = target.GetType();

            MethodInfo[] methodInfo =
                type.GetMethods(
                    BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.FlattenHierarchy
                );

            foreach (var method in methodInfo)
                if (method.IsDefined(typeof(InjectAttribute)))
                    InjectToMethod(method, target);
        }

        private void InjectToMethod(MethodInfo method, object target)
        {
            ParameterInfo[] parameters = method.GetParameters();
            int length = parameters.Length;
            object[] args = new object[length];

            bool hasMissingDependency = false;

            for (int i = 0; i < length; i++)
            {
                ParameterInfo parameterInfo = parameters[i];
                Type type = parameterInfo.ParameterType;

                object service = Get(type);

                if (service == null && _parent != null)
                    service = _parent.Get(type);

                if (service == null)
                {
                    hasMissingDependency = true;
                    Debug.LogError(
                        $"[DiContainer] Missing dependency for parameter '{parameterInfo.Name}' " +
                        $"of type '{type.Name}' in method '{method.Name}' on '{target.GetType().Name}'. " +
                        $"Service not registered in container."
                    );
                }

                args[i] = service;
            }

            // Если хотя бы один параметр не найден — не вызываем метод вообще
            if (hasMissingDependency)
                return;

            method.Invoke(target, args);
        }
    }
}