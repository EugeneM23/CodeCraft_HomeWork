using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>The registry when ids collide: an object outside any scanned scene, a restore onto a taken id, a second unregister.</summary>
    public class SaveRegistryEdgeTests
    {
        private readonly List<GameObject> _objects = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Load();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _objects)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _objects.Clear();
        }

        /// <summary>A Saveable that wakes up with the given id, as a scene object would.</summary>
        private Saveable WithId(string name, string id, bool persistent)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.SetActive(false);
            if (persistent)
                Object.DontDestroyOnLoad(go);

            var saveable = go.AddComponent<Saveable>();
            saveable.SetId(id);
            go.SetActive(true);
            return saveable;
        }

        [UnityTest]
        public IEnumerator Registry_SceneIdTakenOutsideScannedScene_ErrorAndNewSpawnedId()
        {
            LogAssert.Expect(LogType.Error, new Regex($"has the id {TestScenes.BOX} of 'Box'"));
            Saveable twin = WithId("Twin", TestScenes.BOX, persistent: true);

            Assert.AreNotEqual(TestScenes.BOX, twin.Id);
            Assert.IsTrue(twin.IsSpawned);
            Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable box));
            Assert.AreSame(TestScenes.Find("Box"), box.gameObject);
            yield break;
        }

        [UnityTest]
        public IEnumerator Registry_RekeyToTakenId_ErrorAndNewId()
        {
            GameObject instance = Object.Instantiate(TestScenes.Prefab);
            _objects.Add(instance);
            var root = instance.GetComponent<Saveable>();

            LogAssert.Expect(LogType.Error, new Regex($"The saved id {TestScenes.BOX} is taken by 'Box'"));
            SaveRegistry.Rekey(root, TestScenes.BOX);

            Assert.AreNotEqual(TestScenes.BOX, root.Id);
            Assert.IsTrue(SaveRegistry.TryGet(root.Id, out Saveable found));
            Assert.AreSame(root, found);
            Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable box));
            Assert.AreSame(TestScenes.Find("Box"), box.gameObject);
            yield break;
        }

        [UnityTest]
        public IEnumerator Registry_UnregisterTwice_NewOwnerOfTheIdStays()
        {
            var first = new GameObject("First").AddComponent<Saveable>();
            _objects.Add(first.gameObject);
            string id = first.Id;
            SaveRegistry.Unregister(first);
            Assert.IsFalse(SaveRegistry.TryGet(id, out _));
            Saveable second = WithId("Second", id, persistent: false);

            SaveRegistry.Unregister(first);

            Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable owner));
            Assert.AreSame(second, owner);
            Assert.IsFalse(second.IsSpawned, "the id was free again: it is kept as a scene id");
            yield break;
        }
    }
}
