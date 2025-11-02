using System;
using System.Collections.Generic;

namespace Gameplay
{
    public partial class DiContainer
    {
        public void BindSingle<T>(T instance)
        {
            if (_services.ContainsKey(typeof(T)) || _parent?.Contains(typeof(T)) == true)
                throw new InvalidOperationException($"The type {typeof(T)} has already been installed as singleton.");

            _services[typeof(T)] = instance;
        }

        public void BindInterfaceToInstance<TInterface>(object instance)
        {
            if (instance is not TInterface)
                throw new InvalidOperationException($"{instance.GetType()} does not implement {typeof(TInterface)}");

            AddToList((TInterface)instance);
        }

        public void BindInterfacesAndSelf<T>(T instance, bool includeBaseClasses = true)
        {
            BindSingle(instance);

            var interfaces = instance.GetType().GetInterfaces();
            foreach (var interfaceType in interfaces)
            {
                AddToListDynamic(interfaceType, instance);
            }

            if (includeBaseClasses)
            {
                var baseType = instance.GetType().BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    AddToListDynamic(baseType, instance);
                    baseType = baseType.BaseType;
                }
            }
        }

        private void AddToList<T>(T instance)
        {
            var listType = typeof(List<T>);

            if (_services.TryGetValue(listType, out var existing))
                ((List<T>)existing).Add(instance);
            else
                _services[listType] = new List<T> { instance };
        }

        private void AddToListDynamic(Type type, object instance)
        {
            var listType = typeof(List<>).MakeGenericType(type);

            if (_services.TryGetValue(listType, out var existing))
                ((System.Collections.IList)existing).Add(instance);
            else
            {
                var list = (System.Collections.IList)Activator.CreateInstance(listType);
                list.Add(instance);
                _services[listType] = list;
            }
        }
    }
}