using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

namespace Saving.Objects
{
    /// <summary>Writes every UnityEngine.Object as a reference (<see cref="SaveReferences"/>) instead of walking it.</summary>
    internal sealed class UnityReferenceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(Object).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            SaveReferences.Write(value as Object).WriteTo(writer);

        // A reference the save could not express keeps the value already there (when the reader fills an existing object)
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            return SaveReferences.IsUnresolved(token) ? existingValue : SaveReferences.Read(token, objectType);
        }
    }
}
