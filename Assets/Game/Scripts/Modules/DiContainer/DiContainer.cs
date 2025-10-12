using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-900)]
    public class DiContainer : MonoBehaviour
    {
        public static DiContainer Instance { get; private set; }
        public int Count => _services.Count;
        
        private readonly Dictionary<string , object> _services = new();

        [SerializeField] private Installer[] _installers;

        private void Awake()
        {
            Instance = this;
            
            foreach (Installer installer in _installers)
            {
                installer.Install(this);
            }

            MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();

            foreach (var component in allObjects)
                Inject(component);

            foreach (var item in _services)
                Inject(item.Value);
        }

        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            T component = GameObject.Instantiate(prefab, position, rotation, transform);
            Inject(component);
            return component;
        }

        public void Add(Enum id, object service)
        {
            _services[id.ToString()] = service;
        }

        public TContract Get<TContract>(Enum id)
        {
            return (TContract)_services[id.ToString()];
        } 

        public object Get(Type type)
        {
            foreach (var (key, value) in _services)
                if (value.GetType() == type)
                    return value;

            return null;
        }


        public IEnumerable<T> GetAll<T>() => _services.Values.OfType<T>();
        public object[] GetAll() => _services.Values.ToArray();

        private void Inject(object target)
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

                object service = Get(type);

                args[i] = service;
            }

            method.Invoke(target, args);
        }
    }
}