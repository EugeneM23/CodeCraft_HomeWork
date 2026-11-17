using System.Linq;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>What the Saveable inspector lists: every component with its kind, checkbox, mode and members.</summary>
    public class SaveableEditorTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void Rows_EveryComponentWithKindAndCheckbox()
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            root.AddComponent<FieldsProbe>();
            root.AddComponent<SerializerProbe>();
            root.AddComponent<PlainProbe>();
            saveable.SetSaved("SerializerProbe", false);

            var rows = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                .ToDictionary(row => row.Key);

            Assert.AreEqual(SerializerKind.BuiltIn, rows["Transform"].Kind);
            Assert.AreEqual(SerializerKind.Fields, rows["FieldsProbe"].Kind);
            Assert.AreEqual(SerializerKind.Own, rows["SerializerProbe"].Kind);
            Assert.AreEqual(SerializerKind.None, rows["PlainProbe"].Kind);
            Assert.IsFalse(rows["SerializerProbe"].Saved);
            Assert.IsTrue(rows["FieldsProbe"].Saved);
            Assert.IsFalse(rows["PlainProbe"].Saved, "a component without a serializer waits to be checked");
            Assert.IsFalse(rows.ContainsKey("Saveable"));
        }

        [Test]
        public void Rows_ModesAndMembers()
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            root.AddComponent<MembersProbe>();
            root.AddComponent<Light>();
            saveable.SetMode("Light", SaveMode.AllFields);
            saveable.SetMembers("MembersProbe", new[] { "_timer" });

            var rows = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                .ToDictionary(row => row.Key);

            Assert.IsTrue(rows["Light"].Saved);
            Assert.AreEqual(SaveMode.AllFields, rows["Light"].Mode);
            Assert.IsTrue(rows["MembersProbe"].Saved);
            Assert.AreEqual(SaveMode.Selected, rows["MembersProbe"].Mode);
            CollectionAssert.AreEqual(new[] { "_timer" }, rows["MembersProbe"].Members);
            CollectionAssert.Contains(rows["MembersProbe"].Candidates.Select(member => member.Name).ToArray(), "_timer");
            Assert.AreEqual(SaveMode.Auto, rows["Transform"].Mode);
        }

        [Test]
        public void Check_ComponentWithoutSerializer_TurnsOnAllFields()
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            root.AddComponent<PlainProbe>();

            SaveableEditor.SetChecked(saveable, "PlainProbe", SerializerKind.None, true);
            Assert.AreEqual(SaveMode.AllFields, saveable.ModeOf("PlainProbe"));
            Assert.IsTrue(SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                .Single(row => row.Key == "PlainProbe").Saved);

            SaveableEditor.SetChecked(saveable, "PlainProbe", SerializerKind.None, false);
            Assert.AreEqual(SaveMode.Auto, saveable.ModeOf("PlainProbe"));
        }

        [Test]
        public void Check_ComponentWithSerializer_TogglesExclusionOnly()
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();

            SaveableEditor.SetChecked(saveable, "Transform", SerializerKind.BuiltIn, false);
            Assert.IsFalse(saveable.IsSaved("Transform"));

            SaveableEditor.SetChecked(saveable, "Transform", SerializerKind.BuiltIn, true);
            Assert.IsTrue(saveable.IsSaved("Transform"));
            Assert.AreEqual(SaveMode.Auto, saveable.ModeOf("Transform"));
        }

        [Test]
        public void Rows_SerializerOptions_ListedWithState()
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            root.AddComponent<ParticleSystem>();
            saveable.SetOption("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES, true);

            SaveableEditor.Row row = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                .Single(candidate => candidate.Key == "ParticleSystem");

            CollectionAssert.AreEqual(new[] { ParticleSystemSerializer.LIVE_PARTICLES }, row.Options);
            Assert.IsTrue(row.IsOn(ParticleSystemSerializer.LIVE_PARTICLES));
            Assert.IsTrue(saveable.IsOptionOn("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES));
        }
    }
}
