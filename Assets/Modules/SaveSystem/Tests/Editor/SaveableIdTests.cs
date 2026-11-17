using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEditor;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Ids in the editor: new objects get one, duplicates get a new one, unique ones stay.</summary>
    public class SaveableIdTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static Saveable Make(string name) => new GameObject(name).AddComponent<Saveable>();

        private static void SetId(Saveable saveable, string id)
        {
            var serialized = new SerializedObject(saveable);
            serialized.FindProperty("_id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Ids_EmptyId_GetsNewOne()
        {
            Saveable saveable = Make("New");
            SetId(saveable, string.Empty);

            Assert.IsTrue(SaveableIdAssigner.Assign(saveable));
            Assert.AreEqual(32, saveable.Id.Length);
        }

        [Test]
        public void Ids_Duplicate_GetsNewOneAndFirstKeepsIt()
        {
            Saveable first = Make("First");
            Saveable second = Make("Second");
            SetId(first, "11111111111111111111111111111111");
            SetId(second, "11111111111111111111111111111111");

            Assert.IsTrue(SaveableIdAssigner.Assign(second));

            Assert.AreEqual("11111111111111111111111111111111", first.Id);
            Assert.AreNotEqual(first.Id, second.Id);
        }

        [Test]
        public void Ids_UniqueId_Kept()
        {
            Saveable saveable = Make("Unique");
            SetId(saveable, "22222222222222222222222222222222");

            Assert.IsFalse(SaveableIdAssigner.Assign(saveable));
            Assert.AreEqual("22222222222222222222222222222222", saveable.Id);
        }

        [Test]
        public void Ids_AddedInEditor_AssignedAtOnce()
        {
            // No wait for an editor update: a background editor may not run one for a long time
            Saveable saveable = Make("Added");

            Assert.AreEqual(32, saveable.Id?.Length ?? 0);
        }
    }
}
