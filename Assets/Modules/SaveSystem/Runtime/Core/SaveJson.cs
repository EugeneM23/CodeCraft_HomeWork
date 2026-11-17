using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Saving
{
    /// <summary>
    /// The one Newtonsoft setup every save file uses: camelCase members with dictionary keys kept as they are, fresh
    /// collections on read, no type names in the data, strings that look like dates left as strings, Unity structs
    /// written as plain numbers.
    /// </summary>
    public static class SaveJson
    {
        public static readonly JsonSerializerSettings Settings = CreateSettings();
        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static JToken ToToken<T>(T value) =>
            value == null ? JValue.CreateNull() : JToken.FromObject(value, Serializer);

        public static T FromToken<T>(JToken token) =>
            token == null || token.Type == JTokenType.Null ? default : token.ToObject<T>(Serializer);

        public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

        /// <summary>Reads a whole document; false for broken JSON, a wrong shape or an empty text.</summary>
        public static bool TryRead<T>(string text, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                value = JsonConvert.DeserializeObject<T>(text, Settings);
            }
            catch (Exception)
            {
                value = null;
            }

            return value != null;
        }

        /// <summary>Parses a JSON object without turning date-like strings into dates; false for anything else.</summary>
        public static bool TryParseObject(string text, out JObject json)
        {
            json = null;
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
                json = JObject.Load(reader);
                return true;
            }
            catch (Exception)
            {
                json = null;
                return false;
            }
        }

        private static JsonSerializerSettings CreateSettings() => new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true)
            },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            Converters = UnityJsonConverters.Create()
        };
    }
}
