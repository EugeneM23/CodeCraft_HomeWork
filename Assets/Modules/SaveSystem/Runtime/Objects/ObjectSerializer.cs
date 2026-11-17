using System;
using Newtonsoft.Json.Linq;

namespace Saving.Objects
{
    /// <summary>
    /// Saves an object of plain code (or a ScriptableObject, a MonoBehaviour) as a whole section, without a serializer
    /// class: saves.Register("stats", stats). Its [Save] members if it has any, otherwise everything SaveMembers.All
    /// sees; a load fills the same object member by member. References to Unity objects follow the rules of saved
    /// references.
    /// </summary>
    public sealed class ObjectSerializer : IGameSerializer
    {
        private readonly MembersSerializer _members;

        public ObjectSerializer(string key, object target, int order = 0)
        {
            SaveKeys.ValidateUserKey(key);
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Key = key;
            Order = order;
            _members = new MembersSerializer(target.GetType(), SaveMembers.All(target.GetType()));
        }

        public string Key { get; }
        public int Version => 1;
        public int Order { get; }
        public object Target { get; }

        public JToken Capture() => _members.CaptureObject(Target);
        public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);
        public void Restore(JToken data) => _members.RestoreObject(Target, data);
    }
}
