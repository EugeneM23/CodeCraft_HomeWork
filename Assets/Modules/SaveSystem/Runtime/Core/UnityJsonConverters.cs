using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// Unity structs as plain objects of numbers. Without them Newtonsoft walks properties like Vector3.normalized
    /// and loops forever.
    /// </summary>
    public static class UnityJsonConverters
    {
        public static IList<JsonConverter> Create() => new List<JsonConverter>
        {
            new Vector2Converter(),
            new Vector3Converter(),
            new Vector4Converter(),
            new Vector2IntConverter(),
            new Vector3IntConverter(),
            new QuaternionConverter(),
            new ColorConverter(),
            new Color32Converter(),
            new RectConverter(),
            new BoundsConverter(),
            new LayerMaskConverter()
        };

        private abstract class StructConverter<T> : JsonConverter<T> where T : struct
        {
            public sealed override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                Write(writer, value);
                writer.WriteEndObject();
            }

            public sealed override T ReadJson(JsonReader reader, Type objectType, T existingValue,
                bool hasExistingValue, JsonSerializer serializer) => Read(JObject.Load(reader));

            protected abstract void Write(JsonWriter writer, T value);
            protected abstract T Read(JObject json);

            protected static void Number(JsonWriter writer, string name, float value)
            {
                writer.WritePropertyName(name);
                writer.WriteValue(value);
            }

            protected static void Number(JsonWriter writer, string name, int value)
            {
                writer.WritePropertyName(name);
                writer.WriteValue(value);
            }

            protected static float Float(JObject json, string name) => json.Value<float?>(name) ?? 0f;
            protected static int Int(JObject json, string name) => json.Value<int?>(name) ?? 0;
        }

        private sealed class Vector2Converter : StructConverter<Vector2>
        {
            protected override void Write(JsonWriter writer, Vector2 value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
            }

            protected override Vector2 Read(JObject json) => new(Float(json, "x"), Float(json, "y"));
        }

        private sealed class Vector3Converter : StructConverter<Vector3>
        {
            protected override void Write(JsonWriter writer, Vector3 value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "z", value.z);
            }

            protected override Vector3 Read(JObject json) => new(Float(json, "x"), Float(json, "y"), Float(json, "z"));
        }

        private sealed class Vector4Converter : StructConverter<Vector4>
        {
            protected override void Write(JsonWriter writer, Vector4 value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "z", value.z);
                Number(writer, "w", value.w);
            }

            protected override Vector4 Read(JObject json) =>
                new(Float(json, "x"), Float(json, "y"), Float(json, "z"), Float(json, "w"));
        }

        private sealed class Vector2IntConverter : StructConverter<Vector2Int>
        {
            protected override void Write(JsonWriter writer, Vector2Int value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
            }

            protected override Vector2Int Read(JObject json) => new(Int(json, "x"), Int(json, "y"));
        }

        private sealed class Vector3IntConverter : StructConverter<Vector3Int>
        {
            protected override void Write(JsonWriter writer, Vector3Int value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "z", value.z);
            }

            protected override Vector3Int Read(JObject json) => new(Int(json, "x"), Int(json, "y"), Int(json, "z"));
        }

        private sealed class QuaternionConverter : StructConverter<Quaternion>
        {
            protected override void Write(JsonWriter writer, Quaternion value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "z", value.z);
                Number(writer, "w", value.w);
            }

            protected override Quaternion Read(JObject json) =>
                new(Float(json, "x"), Float(json, "y"), Float(json, "z"), Float(json, "w"));
        }

        private sealed class ColorConverter : StructConverter<Color>
        {
            protected override void Write(JsonWriter writer, Color value)
            {
                Number(writer, "r", value.r);
                Number(writer, "g", value.g);
                Number(writer, "b", value.b);
                Number(writer, "a", value.a);
            }

            protected override Color Read(JObject json) =>
                new(Float(json, "r"), Float(json, "g"), Float(json, "b"), Float(json, "a"));
        }

        private sealed class Color32Converter : StructConverter<Color32>
        {
            protected override void Write(JsonWriter writer, Color32 value)
            {
                Number(writer, "r", value.r);
                Number(writer, "g", value.g);
                Number(writer, "b", value.b);
                Number(writer, "a", value.a);
            }

            protected override Color32 Read(JObject json) =>
                new((byte)Int(json, "r"), (byte)Int(json, "g"), (byte)Int(json, "b"), (byte)Int(json, "a"));
        }

        private sealed class RectConverter : StructConverter<Rect>
        {
            protected override void Write(JsonWriter writer, Rect value)
            {
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "width", value.width);
                Number(writer, "height", value.height);
            }

            protected override Rect Read(JObject json) =>
                new(Float(json, "x"), Float(json, "y"), Float(json, "width"), Float(json, "height"));
        }

        private sealed class BoundsConverter : StructConverter<Bounds>
        {
            protected override void Write(JsonWriter writer, Bounds value)
            {
                writer.WritePropertyName("center");
                WriteVector(writer, value.center);
                writer.WritePropertyName("size");
                WriteVector(writer, value.size);
            }

            protected override Bounds Read(JObject json) =>
                new(ReadVector(json["center"] as JObject), ReadVector(json["size"] as JObject));

            private static void WriteVector(JsonWriter writer, Vector3 value)
            {
                writer.WriteStartObject();
                Number(writer, "x", value.x);
                Number(writer, "y", value.y);
                Number(writer, "z", value.z);
                writer.WriteEndObject();
            }

            private static Vector3 ReadVector(JObject json) =>
                json == null ? Vector3.zero : new Vector3(Float(json, "x"), Float(json, "y"), Float(json, "z"));
        }

        /// <summary>A layer mask as its bits, { "value": 5 }: the struct has no field Newtonsoft would see.</summary>
        private sealed class LayerMaskConverter : StructConverter<LayerMask>
        {
            protected override void Write(JsonWriter writer, LayerMask value) => Number(writer, "value", value.value);
            protected override LayerMask Read(JObject json) => Int(json, "value");
        }
    }
}
