using System;
using Newtonsoft.Json.Linq;

namespace Saving
{
    /// <summary>
    /// A value of plain code saved through a getter and a setter, without a serializer class:
    /// saves.Register("volume", () => volume, value => volume = value). The value goes through SaveJson.
    /// </summary>
    public sealed class DelegateSerializer<T> : IGameSerializer
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        public DelegateSerializer(string key, Func<T> get, Action<T> set, int order = 0)
        {
            SaveKeys.ValidateUserKey(key);
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Key = key;
            Order = order;
        }

        public string Key { get; }
        public int Version => 1;
        public int Order { get; }

        public JToken Capture() => SaveJson.ToToken(_get());
        public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);
        public void Restore(JToken data) => _set(SaveJson.FromToken<T>(data));
    }
}
