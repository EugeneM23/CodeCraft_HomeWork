using System.Collections;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>The hidden registry: scene scan with disabled objects, copies and prefab instances, destruction.</summary>
    public class SaveRegistryTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Load();
        }

        [UnityTest]
        public IEnumerator Registry_SceneScan_FindsDisabledObjectsAsOriginals()
        {
            Assert.IsTrue(SaveRegistry.TryGet(TestScenes.HIDDEN, out Saveable hidden));
            Assert.IsFalse(hidden.gameObject.activeSelf);
            Assert.IsFalse(hidden.IsSpawned);

            Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out SaveRegistry.SceneRecord record));
            CollectionAssert.IsSupersetOf(record.Originals, new[] { TestScenes.BOX, TestScenes.HIDDEN, TestScenes.DOOMED });
            yield break;
        }

        [UnityTest]
        public IEnumerator Registry_CopyOfSceneObject_NewIdAndSpawned()
        {
            GameObject box = TestScenes.Find("Box");

            var copy = Object.Instantiate(box).GetComponent<Saveable>();

            Assert.AreNotEqual(TestScenes.BOX, copy.Id);
            Assert.IsTrue(copy.IsSpawned);
            Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable original));
            Assert.AreSame(box, original.gameObject);
            yield break;
        }

        [UnityTest]
        public IEnumerator Registry_PrefabInstance_SpawnedWithPrefabId()
        {
            GameObject instance = Object.Instantiate(TestScenes.Prefab);
            var root = instance.GetComponent<Saveable>();
            var inner = instance.transform.Find("Inner").GetComponent<Saveable>();

            Assert.AreEqual(32, root.Id.Length);
            Assert.IsTrue(root.IsSpawned);
            Assert.AreEqual(TestScenes.GuidOf(TestScenes.PREFAB), root.PrefabId);
            Assert.IsTrue(inner.IsSpawned);
            Assert.AreNotEqual(root.Id, inner.Id);
            yield break;
        }

        [UnityTest]
        public IEnumerator Registry_Destroyed_Unregistered()
        {
            Object.Destroy(TestScenes.Find("Doomed"));
            yield return null;

            Assert.IsFalse(SaveRegistry.TryGet(TestScenes.DOOMED, out _));
        }

        [UnityTest]
        public IEnumerator Registry_Started_SceneNoLongerFresh()
        {
            Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable box));
            Assert.IsTrue(box.HasStarted);
            Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out SaveRegistry.SceneRecord record));
            Assert.IsTrue(record.AnyStarted);
            Assert.IsFalse(record.IsFresh);
            yield break;
        }
    }
}
