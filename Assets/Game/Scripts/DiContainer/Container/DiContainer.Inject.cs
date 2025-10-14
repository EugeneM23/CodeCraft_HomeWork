using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer
    {
        public void Inject(object target)
        {
            if (target == null) return;

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
            var service = Resolve(memberType);
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
                var service = Resolve(type);
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
    }
}