using System.Collections;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>All Fields and Selected through a real save system and a load that reloads the test scene.</summary>
    public class SaveModeSceneTests
    {
        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;
        private GameObject _systemObject;
        private SaveLoadSystem _system;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Load();
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
            _systemObject = new GameObject("SaveLoadSystem");
            Object.DontDestroyOnLoad(_systemObject);
            _system = _systemObject.AddComponent<SaveLoadSystem>();
            _system.UseStorage(_storage);
            _system.UseSettings(_settings);
            _system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_systemObject);
            Object.DestroyImmediate(_settings);
        }

        private IEnumerator LoadAndWait()
        {
            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            _system.Loaded += OnLoaded;
            Assert.AreEqual(LoadOutcome.Loaded, _system.Load());
            yield return TestScenes.WaitFor(() => done);
            _system.Loaded -= OnLoaded;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Modes_LiveObject_GetsAllFieldsAndSelectedBack()
        {
            GameObject keeper = Object.Instantiate(TestScenes.Prefab);
            Object.DontDestroyOnLoad(keeper);
            var probe = keeper.AddComponent<MembersProbe>();
            var light = keeper.AddComponent<Light>();
            var saveable = keeper.GetComponent<Saveable>();
            saveable.SetMode("MembersProbe", SaveMode.AllFields);
            saveable.SetMembers("Light", new[] { "intensity", "color" });
            yield return null;

            probe.Health = 3;
            probe.Ids.Add(7);
            light.intensity = 5f;
            light.color = Color.green;
            light.range = 4f;
            _system.Save();
            probe.Health = 10;
            probe.Ids.Clear();
            light.intensity = 1f;
            light.color = Color.white;
            light.range = 9f;

            yield return LoadAndWait();

            Assert.AreEqual(3, probe.Health);
            CollectionAssert.AreEqual(new[] { 7 }, probe.Ids);
            Assert.AreEqual(5f, light.intensity);
            Assert.AreEqual(Color.green, light.color);
            Assert.AreEqual(9f, light.range, "range was not selected");
            Object.Destroy(keeper);
        }

        [UnityTest]
        public IEnumerator Values_SurviveSceneReload()
        {
            _system.Values.Set("coins", 25);
            _system.Save();
            _system.Values.Set("coins", 0);

            yield return LoadAndWait();

            Assert.AreEqual(25, _system.Values.Get("coins", -1));
        }
    }
}
