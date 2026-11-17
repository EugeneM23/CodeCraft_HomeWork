using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>
    /// Serializers and registration shortcuts at their edges: null services, missing migrations, data of the wrong
    /// shape, a member that throws, two serializers for one type.
    /// </summary>
    public class SerializerGuardTests
    {
        private sealed class WithBrokenGetter
        {
            public int Fine { get; set; }

            public int Broken
            {
                get => throw new InvalidOperationException("broken getter");
                set { }
            }
        }

        // ---------- Shortcuts ----------

        [Test]
        public void RegisterShortcuts_NullService_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SaveLoadServiceExtensions.Register<int>(null, "volume", () => 1, _ => { }));
            Assert.Throws<ArgumentNullException>(() => SaveObjectsExtensions.Register(null, "stats", new object()));
        }

        // ---------- Missing migrations ----------

        [Test]
        public void DelegateSerializer_Upgrade_ThrowsWithKeyAndVersion()
        {
            var serializer = new DelegateSerializer<int>("volume", () => 1, _ => { });

            var exception = Assert.Throws<SaveMigrationException>(() => serializer.Upgrade(new JValue(1), 2));

            Assert.AreEqual("volume", exception.Key);
            Assert.AreEqual(2, exception.FromVersion);
        }

        [Test]
        public void ObjectSerializer_Upgrade_Throws()
        {
            var serializer = new ObjectSerializer("stats", new WithBrokenGetter());

            var exception = Assert.Throws<SaveMigrationException>(() => serializer.Upgrade(new JObject(), 1));

            Assert.AreEqual("stats", exception.Key);
        }

        [Test]
        public void SaveValues_Upgrade_Throws()
        {
            IGameSerializer values = new SaveValues();

            var exception = Assert.Throws<SaveMigrationException>(() => values.Upgrade(new JObject(), 1));

            Assert.AreEqual(SaveValues.KEY, exception.Key);
        }

        [Test]
        public void FieldsAndMembersSerializers_Upgrade_Throw()
        {
            Assert.Throws<SaveMigrationException>(() => FieldsSerializer.For(typeof(FieldsProbe)).Upgrade(new JObject(), 1));
            Assert.Throws<SaveMigrationException>(() => MembersSerializer.AllFor(typeof(MembersProbe)).Upgrade(new JObject(), 1));
        }

        // ---------- Data of the wrong shape ----------

        [Test]
        public void SaveValues_RestoreNotAnObject_ClearsValues()
        {
            var values = new SaveValues();
            values.Set("coins", 3);

            ((IGameSerializer)values).Restore(new JValue(5));

            Assert.AreEqual(0, values.Count);
        }

        [Test]
        public void FieldsSerializer_RestoreNotAnObject_Throws() =>
            Assert.Throws<FormatException>(() => FieldsSerializer.For(typeof(FieldsProbe)).Restore(null, new JValue(5)));

        [Test]
        public void MembersSerializer_RestoreNotAnObject_Throws()
        {
            var serializer = new MembersSerializer(typeof(WithBrokenGetter), SaveMembers.All(typeof(WithBrokenGetter)));

            Assert.Throws<FormatException>(() => serializer.RestoreObject(new WithBrokenGetter(), new JArray()));
            Assert.Throws<FormatException>(() => serializer.RestoreObject(new WithBrokenGetter(), null));
        }

        [Test]
        public void FieldsSerializer_NullType_None() => Assert.IsNull(FieldsSerializer.For(null));

        // ---------- Members that throw ----------

        [Test]
        public void MembersSerializer_GetterThrows_MemberSkippedOthersCaptured()
        {
            var serializer = new MembersSerializer(typeof(WithBrokenGetter), SaveMembers.All(typeof(WithBrokenGetter)));
            CollectionAssert.AreEquivalent(new[] { "Fine", "Broken" }, new[] { serializer.Members[0].Name, serializer.Members[1].Name });

            // The warning comes once per member and domain: it is not asserted, the result is
            JObject json = serializer.CaptureObject(new WithBrokenGetter { Fine = 3 });

            Assert.AreEqual(3, (int)json["Fine"]);
            Assert.IsNull(json["Broken"]);
        }

        // ---------- Catalog ----------

        [Test]
        public void Catalog_NullType_None()
        {
            var catalog = new ComponentSerializerCatalog(null, null);

            Assert.IsNull(catalog.For(null));
            Assert.AreEqual(SerializerKind.Fields, catalog.KindOf(typeof(FieldsProbe)), "no lists: [Save] fields still work");
        }

        [Test]
        public void Catalog_TwoSerializersForOneType_FirstUsedWithError()
        {
            var first = new TaggedSerializer(typeof(Transform), "first");
            var second = new TaggedSerializer(typeof(Transform), "second");

            LogAssert.Expect(LogType.Error, new Regex("Two serializers for Transform"));
            var catalog = new ComponentSerializerCatalog(new IComponentSerializer[] { first, second }, new IComponentSerializer[0]);

            Assert.AreSame(first, catalog.For(typeof(Transform)));
        }
    }
}
