using System.Linq;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// The explicit list of a Saveable: what a Saveable added in the editor starts with, checking and unchecking, and
    /// switching an old "everything unless unchecked" Saveable over.
    /// </summary>
    public class SaveableListTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            Saveable.DefaultsOnAdd = true; // off for the rest of the assembly (OldSaveableDefaults)
        }

        [TearDown]
        public void TearDown()
        {
            Saveable.DefaultsOnAdd = false;
            _scene.Dispose();
        }

        private static ComponentSerializerCatalog Catalog => ComponentSerializerCatalog.Default;

        private static GameObject MakeHero()
        {
            var hero = new GameObject("Hero");
            hero.AddComponent<Rigidbody2D>();
            hero.AddComponent<BoxCollider2D>();
            hero.AddComponent<MembersProbe>();   // a script without [Save]
            hero.AddComponent<SaveTestCounter>(); // a script with [Save]
            hero.AddComponent<Light>();
            var child = new GameObject("Child");
            child.transform.SetParent(hero.transform, false);
            child.AddComponent<SaveTestCounter>();
            return hero;
        }

        [Test]
        public void AddedInTheEditor_StartsWithItsOwnTransformBodyAndScripts()
        {
            var saveable = MakeHero().AddComponent<Saveable>();

            Assert.IsTrue(saveable.IsExplicit);
            CollectionAssert.AreEquivalent(new[] { "Transform", "Rigidbody2D", "MembersProbe", "SaveTestCounter" },
                ObjectState.Capture(saveable, Catalog).Components.Keys.ToArray());
            Assert.AreEqual(SaveMode.AllFields, saveable.ModeOf("MembersProbe"), "no serializer, no [Save]: all its fields");
            Assert.AreEqual(SaveMode.Auto, saveable.ModeOf("SaveTestCounter"), "[Save] members as they are");
            Assert.IsFalse(saveable.IsSaved("BoxCollider2D"), "\"collider enabled\" is rarely state");
            Assert.IsFalse(saveable.IsSaved("Light"));
            Assert.IsFalse(saveable.IsSaved("Child/Transform"), "children are checked by hand");
            Assert.IsFalse(saveable.IsSaved("Child/SaveTestCounter"));
        }

        [Test]
        public void Explicit_CheckAndUncheck_OnlyTheListIsSaved()
        {
            var saveable = MakeHero().AddComponent<Saveable>();

            saveable.SetSaved("Transform", false);
            saveable.SetSaved("Child/SaveTestCounter", true);
            SaveableEditor.SetChecked(saveable, "Light", SerializerKind.None, true);
            SaveableEditor.SetChecked(saveable, "MembersProbe", SerializerKind.None, false);

            CollectionAssert.AreEquivalent(new[] { "Rigidbody2D", "SaveTestCounter", "Child/SaveTestCounter", "Light" },
                ObjectState.Capture(saveable, Catalog).Components.Keys.ToArray());
            Assert.IsFalse(saveable.IsSaved("MembersProbe"), "unchecked: off the list, not just without a mode");
        }

        [Test]
        public void Explicit_ComponentAddedLater_IsNotSavedUntilChecked()
        {
            var saveable = MakeHero().AddComponent<Saveable>();

            saveable.gameObject.AddComponent<AudioSource>();

            Assert.IsFalse(ObjectState.Capture(saveable, Catalog).Components.ContainsKey("AudioSource"));
        }

        [Test]
        public void UseExplicitList_WithKeys_HoldsExactlyThem()
        {
            var saveable = MakeHero().AddComponent<Saveable>();

            saveable.UseExplicitList(new[] { "Child/SaveTestCounter", "Child/SaveTestCounter", null });

            CollectionAssert.AreEquivalent(new[] { "Child/SaveTestCounter" },
                ObjectState.Capture(saveable, Catalog).Components.Keys.ToArray());
        }

        [Test]
        public void OldSaveable_SavesEverythingThatCanBeSaved_UntilSwitched()
        {
            Saveable.DefaultsOnAdd = false;
            var saveable = MakeHero().AddComponent<Saveable>();
            saveable.SetMembers("Child/SaveTestCounter", new[] { "Value" }); // set up by hand
            saveable.SetMode("Light", SaveMode.AllFields);
            saveable.SetSaved("Light", false);                              // given a mode, then unchecked

            Assert.IsFalse(saveable.IsExplicit);
            Assert.IsTrue(ObjectState.Capture(saveable, Catalog).Components.ContainsKey("Child/Transform"), "the old way");

            saveable.UseExplicitList();

            Assert.IsTrue(saveable.IsExplicit);
            CollectionAssert.AreEquivalent(
                new[] { "Transform", "Rigidbody2D", "MembersProbe", "SaveTestCounter", "Child/SaveTestCounter" },
                ObjectState.Capture(saveable, Catalog).Components.Keys.ToArray());
            Assert.AreEqual(SaveMode.Selected, saveable.ModeOf("Child/SaveTestCounter"), "what was set up by hand stays");
        }
    }
}
