using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Component type -> serializer. <see cref="Default"/> finds every IComponentSerializer with a public
    /// parameterless constructor in the assemblies that reference SaveSystem.Objects. Priority: own serializer,
    /// built-in (this assembly), [Save] fields. A serializer of a base type serves its subclasses unless a closer
    /// one exists.
    /// </summary>
    public sealed class ComponentSerializerCatalog
    {
        private static ComponentSerializerCatalog _default;

        private readonly Dictionary<Type, IComponentSerializer> _own = new();
        private readonly Dictionary<Type, IComponentSerializer> _builtIn = new();
        private readonly Dictionary<Type, IComponentSerializer> _resolved = new();

        public ComponentSerializerCatalog(IEnumerable<IComponentSerializer> own, IEnumerable<IComponentSerializer> builtIn)
        {
            Add(own, _own);
            Add(builtIn, _builtIn);
        }

        public static ComponentSerializerCatalog Default => _default ??= Discover();

        public IComponentSerializer For(Type componentType)
        {
            if (componentType == null)
                return null;

            if (!_resolved.TryGetValue(componentType, out IComponentSerializer serializer))
            {
                serializer = Closest(_own, componentType) ?? Closest(_builtIn, componentType) ??
                             FieldsSerializer.For(componentType);
                _resolved.Add(componentType, serializer); // a null answer is cached too
            }

            return serializer;
        }

        public SerializerKind KindOf(Type componentType)
        {
            IComponentSerializer serializer = For(componentType);
            if (serializer == null)
                return SerializerKind.None;

            if (serializer is FieldsSerializer)
                return SerializerKind.Fields;

            return _own.ContainsValue(serializer) ? SerializerKind.Own : SerializerKind.BuiltIn;
        }

        /// <summary>The data version written for a component type name; 1 for [Save] fields and unknown names.</summary>
        public int VersionOf(string typeName)
        {
            foreach (Dictionary<Type, IComponentSerializer> serializers in new[] { _own, _builtIn })
            {
                foreach (KeyValuePair<Type, IComponentSerializer> pair in serializers)
                {
                    if (pair.Key.Name == typeName)
                        return pair.Value.Version;
                }
            }

            return 1;
        }

        private static IComponentSerializer Closest(Dictionary<Type, IComponentSerializer> serializers, Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (serializers.TryGetValue(current, out IComponentSerializer serializer))
                    return serializer;
            }

            return null;
        }

        private static void Add(IEnumerable<IComponentSerializer> serializers, Dictionary<Type, IComponentSerializer> into)
        {
            if (serializers == null)
                return;

            foreach (IComponentSerializer serializer in serializers
                         .Where(serializer => serializer?.ComponentType != null)
                         .OrderBy(serializer => serializer.GetType().FullName, StringComparer.Ordinal))
            {
                if (!into.TryAdd(serializer.ComponentType, serializer))
                    Debug.LogError($"Two serializers for {serializer.ComponentType.Name}: " +
                                   $"{into[serializer.ComponentType].GetType().Name} is used, {serializer.GetType().Name} is not.");
            }
        }

        private static ComponentSerializerCatalog Discover()
        {
            Assembly own = typeof(ComponentSerializerCatalog).Assembly;
            string ownName = own.GetName().Name;
            var game = new List<IComponentSerializer>();
            var builtIn = new List<IComponentSerializer>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != own && assembly.GetReferencedAssemblies().All(reference => reference.Name != ownName))
                    continue;

                foreach (Type type in TypesOf(assembly))
                {
                    if (type.IsAbstract || type.IsGenericTypeDefinition || !typeof(IComponentSerializer).IsAssignableFrom(type)
                        || type.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    try
                    {
                        (assembly == own ? builtIn : game).Add((IComponentSerializer)Activator.CreateInstance(type));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"The component serializer {type.Name} could not be created: {exception.Message}");
                    }
                }
            }

            return new ComponentSerializerCatalog(game, builtIn);
        }

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
