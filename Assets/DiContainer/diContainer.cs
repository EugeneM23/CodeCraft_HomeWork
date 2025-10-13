using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public class diContainer
    {
        private readonly Dictionary<Type, object> _services = new();

        private Installer[] _installers;

        private readonly diContainer _parent;

        public diContainer(diContainer parent)
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

            int lenth = parameters.Length;
            object[] args = new object[lenth];

            for (int i = 0; i < lenth; i++)
            {
                ParameterInfo parameterInfo = parameters[i];
                Type type = parameterInfo.ParameterType;

                object service = Get(type.Log());

                if (service == null && _parent != null)
                    service = _parent.Get(type);


                /*if (service == null)
                    Debug.LogError($"Can not find service of type {type}");*/

                args[i] = service;
            }

            method.Invoke(target, args);
        }
    }
}