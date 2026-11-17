using System;
using Newtonsoft.Json.Linq;

namespace Saving
{
    /// <summary>
    /// Typed serializer of one global system: the subclass turns the service into a data object and back, the base
    /// writes it as JSON under the key. Bump the version when the data changes shape and override Upgrade with one
    /// step per version.
    /// </summary>
    public abstract class GameSerializer<TService, TData> : IGameSerializer
    {
        protected GameSerializer(TService service, string key, int version = 1, int order = 0)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            SaveKeys.ValidateUserKey(key);
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version), version, "The version starts at 1.");

            Service = service;
            Key = key;
            Version = version;
            Order = order;
        }

        public string Key { get; }
        public int Version { get; }
        public int Order { get; }
        protected TService Service { get; }

        JToken IGameSerializer.Capture() => SaveJson.ToToken(Serialize(Service));
        void IGameSerializer.Restore(JToken data) => Deserialize(Service, SaveJson.FromToken<TData>(data));
        JToken IGameSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

        protected abstract TData Serialize(TService service);
        protected abstract void Deserialize(TService service, TData data);

        /// <summary>One migration step: data written by version <paramref name="fromVersion"/> to the next one.</summary>
        protected virtual JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);
    }
}
