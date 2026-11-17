using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>The shared Newtonsoft setup: Unity structs, naming, fresh collections, strings that look like dates.</summary>
    public class SaveJsonTests
    {
        private sealed class AllStructs
        {
            public Vector2 Flat;
            public Vector3 Point;
            public Vector4 Wide;
            public Vector2Int Cell;
            public Vector3Int Voxel;
            public Quaternion Rotation;
            public Color Tint;
            public Color32 Tint32;
            public Rect Area;
            public Bounds Box;
        }

        private sealed class Naming
        {
            public int SomeValue;
            public Dictionary<string, int> Scores = new();
        }

        private sealed class WithList
        {
            public List<int> Items = new() { 1, 2 };
        }

        private sealed class Note
        {
            public string Text;
        }

        [Test]
        public void SaveJson_UnityStructs_RoundTrip()
        {
            var source = new AllStructs
            {
                Flat = new Vector2(1.5f, -2f),
                Point = new Vector3(1f, 2f, 3.25f),
                Wide = new Vector4(1f, 2f, 3f, 4f),
                Cell = new Vector2Int(3, -4),
                Voxel = new Vector3Int(5, 6, -7),
                Rotation = Quaternion.Euler(10f, 20f, 30f),
                Tint = new Color(0.1f, 0.2f, 0.3f, 0.4f),
                Tint32 = new Color32(10, 20, 30, 40),
                Area = new Rect(1f, 2f, 3f, 4f),
                Box = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f))
            };

            AllStructs copy = SaveJson.FromToken<AllStructs>(SaveJson.ToToken(source));

            Assert.AreEqual(source.Flat, copy.Flat);
            Assert.AreEqual(source.Point, copy.Point);
            Assert.AreEqual(source.Wide, copy.Wide);
            Assert.AreEqual(source.Cell, copy.Cell);
            Assert.AreEqual(source.Voxel, copy.Voxel);
            Assert.AreEqual(source.Rotation, copy.Rotation);
            Assert.AreEqual(source.Tint, copy.Tint);
            Assert.AreEqual(source.Tint32, copy.Tint32);
            Assert.AreEqual(source.Area, copy.Area);
            Assert.AreEqual(source.Box, copy.Box);
        }

        [Test]
        public void SaveJson_Vector3_WrittenAsThreeNumbers()
        {
            var json = (JObject)SaveJson.ToToken(new Vector3(1f, 2f, 3f));

            Assert.AreEqual(3, json.Count);
            Assert.AreEqual(2f, (float)json["y"]);
        }

        [Test]
        public void SaveJson_Members_CamelCaseAndDictionaryKeysAsIs()
        {
            var naming = new Naming { SomeValue = 1 };
            naming.Scores["Player_One"] = 5;

            var json = (JObject)SaveJson.ToToken(naming);

            Assert.IsNotNull(json["someValue"]);
            Assert.AreEqual(5, (int)json["scores"]["Player_One"]);
        }

        [Test]
        public void SaveJson_ListWithInitializer_IsReplacedNotAppended()
        {
            WithList read = SaveJson.FromToken<WithList>(JObject.Parse("{ \"items\": [ 3 ] }"));

            CollectionAssert.AreEqual(new[] { 3 }, read.Items);
        }

        [Test]
        public void SaveJson_DateLikeString_StaysString()
        {
            Assert.IsTrue(SaveJson.TryParseObject("{ \"text\": \"2026-10-23T14:05:00Z\" }", out JObject json));

            Assert.AreEqual(JTokenType.String, json["text"].Type);
            Assert.AreEqual("2026-10-23T14:05:00Z", SaveJson.FromToken<Note>(json).Text);
        }

        [Test]
        public void SaveJson_WriteThenRead_RoundTrips()
        {
            string text = SaveJson.Write(new Note { Text = "hello" });

            Assert.IsTrue(SaveJson.TryRead(text, out Note note));
            Assert.AreEqual("hello", note.Text);
        }

        [Test]
        public void SaveJson_BrokenOrEmptyText_ReadsNothing()
        {
            Assert.IsFalse(SaveJson.TryRead("{ broken", out Note _));
            Assert.IsFalse(SaveJson.TryRead("", out Note _));
            Assert.IsFalse(SaveJson.TryParseObject("[ 1, 2 ]", out JObject _));
        }

        [Test]
        public void SaveJson_LayerMask_KeepsItsBits()
        {
            LayerMask mask = 5;

            JToken token = SaveJson.ToToken(mask);

            Assert.AreEqual(5, (int)token["value"]);
            Assert.AreEqual(5, SaveJson.FromToken<LayerMask>(token).value);
        }
    }
}
