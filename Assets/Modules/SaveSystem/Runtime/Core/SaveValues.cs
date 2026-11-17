using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Saving
{
    /// <summary>
    /// Values of the current slot under string keys: the simplest way to save something from plain code
    /// (Values.Set("coins", 120), Values.Get("coins", 0)). They live in the "$vars" section (not "$values": Newtonsoft
    /// reads that name as its own metadata, so keys starting with '$' are refused here too): selecting a slot starts
    /// them empty, a load replaces them. A value is copied on Set and written through SaveJson, so anything a section
    /// can hold works; Unity object references do not.
    /// </summary>
    public sealed class SaveValues : IGameSerializer
    {
        public const string KEY = SaveKeys.RESERVED_PREFIX + "vars";

        // Sorted: the same values give the same text, so "save only if changed" stays quiet
        private readonly SortedDictionary<string, JToken> _values = new(StringComparer.Ordinal);

        public int Count => _values.Count;
        public IReadOnlyCollection<string> Keys => _values.Keys;

        string IGameSerializer.Key => KEY;
        int IGameSerializer.Version => 1;
        int IGameSerializer.Order => int.MinValue; // values first: other systems may read them on restore

        public void Set<T>(string key, T value)
        {
            ValidateKey(key);
            _values[key] = SaveJson.ToToken(value);
        }

        /// <summary>The value under the key, or <paramref name="defaultValue"/> when it is missing or of another type.</summary>
        public T Get<T>(string key, T defaultValue = default) => TryGet(key, out T value) ? value : defaultValue;

        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (key == null || !_values.TryGetValue(key, out JToken token))
                return false;

            try
            {
                value = SaveJson.FromToken<T>(token);
                return true;
            }
            catch (Exception)
            {
                value = default;
                return false;
            }
        }

        public bool Has(string key) => key != null && _values.ContainsKey(key);

        public bool Delete(string key) => key != null && _values.Remove(key);

        public void Clear() => _values.Clear();

        JToken IGameSerializer.Capture()
        {
            var json = new JObject();
            foreach (KeyValuePair<string, JToken> pair in _values)
                json[pair.Key] = pair.Value.DeepClone();

            return json;
        }

        JToken IGameSerializer.Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(KEY, fromVersion);

        void IGameSerializer.Restore(JToken data)
        {
            _values.Clear();
            if (data is not JObject json)
                return;

            foreach (KeyValuePair<string, JToken> pair in json)
                _values[pair.Key] = pair.Value?.DeepClone() ?? JValue.CreateNull();
        }

        private static void ValidateKey(string key) => SaveKeys.ValidateUserKey(key);
    }
}
