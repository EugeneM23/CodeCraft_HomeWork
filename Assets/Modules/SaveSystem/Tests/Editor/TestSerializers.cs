using System;
using Newtonsoft.Json.Linq;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A serializer for any type, told apart by its tag; can throw on capture. Its constructor takes arguments, so
    /// the catalog never discovers it on its own: tests hand it in explicitly.
    /// </summary>
    internal sealed class TaggedSerializer : IComponentSerializer
    {
        public TaggedSerializer(Type componentType, string tag = "", bool failCapture = false)
        {
            ComponentType = componentType;
            Tag = tag;
            FailCapture = failCapture;
        }

        public Type ComponentType { get; }
        public int Version => 1;
        public string Tag { get; }
        public bool FailCapture { get; set; }
        public string RestoredWith { get; private set; }

        public JToken Capture(Component component) =>
            FailCapture ? throw new InvalidOperationException("capture broke") : new JValue(Tag);

        public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(ComponentType.Name, fromVersion);

        public void Restore(Component component, JToken data) => RestoredWith = (string)data;
    }
}
