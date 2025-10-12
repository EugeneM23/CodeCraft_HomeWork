using System;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-900)]
    public class SceneResolver : MonoBehaviour
    {
        private void Awake()
        {
            foreach (var item in ServiceLocator.GetAll())
                Inject(item);

            MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();
            
            foreach (var component in allObjects) 
                Inject(component);
        }

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

                object o = ServiceLocator.Get(type);

                args[i] = o;
            }

            method.Invoke(target, args);
        }
    }
}