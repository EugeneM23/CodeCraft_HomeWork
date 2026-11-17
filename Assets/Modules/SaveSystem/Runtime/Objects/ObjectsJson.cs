using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// JSON for values of [Save] fields: the SaveJson setup, except that classes follow Unity's rules (public and
    /// [SerializeField] fields, no properties), so a [Serializable] class keeps what the Inspector shows, and Unity
    /// objects as references.
    /// </summary>
    internal static class ObjectsJson
    {
        public static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new UnityFieldsResolver(),
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            Converters = Converters()
        });

        /// <summary>For the data of component serializers: the SaveJson setup plus references.</summary>
        public static readonly JsonSerializer Components = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = SaveJson.Settings.ContractResolver,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            Converters = Converters()
        });

        public static JToken ComponentToToken<T>(T value) =>
            value == null ? JValue.CreateNull() : JToken.FromObject(value, Components);

        public static T ComponentFromToken<T>(JToken token) =>
            token == null || token.Type == JTokenType.Null ? default : token.ToObject<T>(Components);

        private static IList<JsonConverter> Converters()
        {
            IList<JsonConverter> converters = UnityJsonConverters.Create();
            converters.Add(new UnityReferenceConverter());
            return converters;
        }

        public static JToken ToToken(object value) =>
            value == null ? JValue.CreateNull() : JToken.FromObject(value, Serializer);

        public static object FromToken(JToken token, Type type)
        {
            if (token == null || token.Type == JTokenType.Null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            return token.ToObject(type, Serializer);
        }

        private sealed class UnityFieldsResolver : DefaultContractResolver
        {
            private const BindingFlags FIELDS =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            public UnityFieldsResolver() =>
                NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true);

            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = new List<MemberInfo>();
                for (Type current = objectType; current != null && current != typeof(object); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(FIELDS))
                    {
                        if (field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute)))
                            continue;

                        if (field.IsPublic || field.IsDefined(typeof(SerializeField)))
                            members.Add(field);
                    }
                }

                return members;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                // Private [SerializeField] fields are read and written too
                property.Readable = true;
                property.Writable = true;
                return property;
            }
        }
    }
}
