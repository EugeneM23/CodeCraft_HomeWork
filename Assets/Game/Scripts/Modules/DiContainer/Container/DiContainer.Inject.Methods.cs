using System;
using System.Reflection;

namespace Gameplay
{
    public partial class DiContainer
    {
        private void InjectMethods(object target)
        {
            Type type = target.GetType();
            
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (var method in methods)
            {
                if (method.IsDefined(typeof(InjectAttribute), true)) 
                    InjectMethod(method, target);
            }
        }

        private void InjectMethod(MethodInfo method, object target)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type type = parameters[i].ParameterType;
                object service = Resolve(type);

                if (service == null)
                {
                    throw new InvalidOperationException(
                        $"[DiContainer] Missing dependency: '{type.Name}' for '{target.GetType().Name}.{method.Name}'");
                }

                args[i] = service;
            }

            method.Invoke(target, args);
        }
    }
}