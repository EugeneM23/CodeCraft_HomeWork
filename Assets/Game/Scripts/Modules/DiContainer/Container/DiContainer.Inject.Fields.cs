using System;
using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer
    {
        private void InjectFields(object target)
        {
            var type = target.GetType();
            var fields = type.GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (field.IsDefined(typeof(InjectAttribute), true))
                {
                    InjectField(field, target);
                }
            }
        }

        private void InjectField(FieldInfo field, object target)
        {
            object service = Resolve(field.FieldType);
            
            if (service != null)
                field.SetValue(target, service);
            else
            {
                Debug.LogError($"[DiContainer] Missing dependency: '{field.FieldType.Name}'");
            }
        }
    }
}