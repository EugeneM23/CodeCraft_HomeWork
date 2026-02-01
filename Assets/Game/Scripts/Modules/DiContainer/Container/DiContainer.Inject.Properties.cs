using System.Reflection;
using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer
    {
        private void InjectProperties(object target)
        {
            var type = target.GetType();

            var properties = type.GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (var prop in properties)
            {
                if (prop.IsDefined(typeof(InjectAttribute), true) && prop.CanWrite)
                    InjectProperty(prop, target);
            }
        }

        private void InjectProperty(PropertyInfo prop, object target)
        {
            var service = Resolve(prop.PropertyType);

            if (service != null)
                prop.SetValue(target, service);
            else
                Debug.LogError($"[DiContainer] Missing dependency: '{prop.PropertyType.Name}'");
        }
    }
}