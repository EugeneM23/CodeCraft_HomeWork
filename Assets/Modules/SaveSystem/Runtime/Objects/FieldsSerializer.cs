using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Saves the [Save] fields and properties of one component type, inherited ones too, under their names or keys
    /// (<see cref="SaveMembers.Marked"/>). On load every present member is converted first and assigned only when all
    /// of them could be read: a member whose type changed leaves the component as it was. Unity objects are saved as
    /// references; a member holding one the save cannot express keeps what the scene has there.
    /// </summary>
    internal sealed class FieldsSerializer : IComponentSerializer
    {
        private static readonly Dictionary<Type, FieldsSerializer> Cache = new();

        private readonly IReadOnlyList<SaveMember> _members;

        private FieldsSerializer(Type type, IReadOnlyList<SaveMember> members)
        {
            ComponentType = type;
            _members = members;
        }

        public Type ComponentType { get; }
        public int Version => 1;

        /// <summary>The serializer of a type, or null when it has no [Save] member to save.</summary>
        public static FieldsSerializer For(Type type)
        {
            if (type == null)
                return null;

            if (!Cache.TryGetValue(type, out FieldsSerializer serializer))
            {
                serializer = new FieldsSerializer(type, SaveMembers.Marked(type));
                Cache.Add(type, serializer);
            }

            return serializer._members.Count > 0 ? serializer : null;
        }

        public JToken Capture(Component component)
        {
            var json = new JObject();
            foreach (SaveMember member in _members)
            {
                JToken token = ObjectsJson.ToToken(member.GetValue(component));
                json[member.Key] = member.IsReference && SaveReferences.ContainsUnresolved(token)
                    ? SaveReferences.UnresolvedToken()
                    : token;
            }

            return json;
        }

        public JToken Upgrade(JToken data, int fromVersion) =>
            throw new SaveMigrationException(ComponentType.Name, fromVersion);

        public void Restore(Component component, JToken data)
        {
            if (data is not JObject json)
                throw new FormatException($"the [Save] fields of {ComponentType.Name} are not a JSON object");

            var values = new List<(SaveMember Member, object Value)>();
            foreach (SaveMember member in _members)
            {
                // A member that holds a reference the save could not express keeps what the scene gave it
                if (!json.TryGetValue(member.Key, out JToken token)
                    || (member.IsReference && SaveReferences.ContainsUnresolved(token)))
                    continue;

                int notFound = SaveReferences.NotFound;
                object value = ObjectsJson.FromToken(token, member.Type);
                // A list that lost one of its references on the way back is not what the game saved
                if (member.IsReference && SaveReferences.HasHole(member, notFound, ComponentType.Name))
                    continue;

                values.Add((member, value));
            }

            foreach ((SaveMember member, object value) in values)
                member.SetValue(component, value);
        }
    }
}
