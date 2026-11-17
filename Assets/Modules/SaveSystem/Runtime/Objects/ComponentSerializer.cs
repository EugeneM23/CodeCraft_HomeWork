using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Typed serializer of one component type, found by the plugin on its own: the subclass turns the component into
    /// a data object and back, and wins over the built-in serializer and over [Save] fields. Unity objects in TData are
    /// saved as references. Bump Version when the data changes shape and override Upgrade with one step per version.
    /// Needs a public parameterless constructor.
    /// </summary>
    public abstract class ComponentSerializer<TComponent, TData> : IComponentSerializer where TComponent : Component
    {
        public Type ComponentType => typeof(TComponent);
        public virtual int Version => 1;

        JToken IComponentSerializer.Capture(Component component) => ObjectsJson.ComponentToToken(Serialize((TComponent)component));

        void IComponentSerializer.Restore(Component component, JToken data) =>
            Deserialize((TComponent)component, ObjectsJson.ComponentFromToken<TData>(data));

        JToken IComponentSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

        protected abstract TData Serialize(TComponent component);
        protected abstract void Deserialize(TComponent component, TData data);

        /// <summary>One migration step: data written by version <paramref name="fromVersion"/> to the next one.</summary>
        protected virtual JToken Upgrade(JToken data, int fromVersion) =>
            throw new SaveMigrationException(typeof(TComponent).Name, fromVersion);
    }
}
