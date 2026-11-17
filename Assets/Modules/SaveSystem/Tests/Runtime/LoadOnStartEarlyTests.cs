using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// Load On Start and code that registers while the scene wakes (a DI container, Awake, OnEnable): it gets the
    /// saved data the moment it registers, not a frame later in Start — a view that reads the state in OnEnable shows
    /// the saved game from the first frame.
    /// </summary>
    public class LoadOnStartEarlyTests
    {
        private InMemorySaveStorage _storage;
        private GameObject _object;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            // A slot saved in an earlier run
            var earlier = new SaveLoadService(_storage, Application.version);
            int saved = 77;
            earlier.Register("coins", () => saved, value => saved = value);
            earlier.Values.Set("level", 3);
            Assert.AreEqual(SaveOutcome.Saved, earlier.Save());
        }

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
                Object.DestroyImmediate(_object);
        }

        private SaveLoadSystem Create(bool loadOnStart)
        {
            _object = new GameObject("SaveLoadSystem");
            var system = _object.AddComponent<SaveLoadSystem>();
            system.UseStorage(_storage);
            system.Configure(loadOnStart: loadOnStart, saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            return system;
        }

        [UnityTest]
        public IEnumerator LoadOnStart_RegisteredBeforeStart_GetsTheDataAtOnce()
        {
            SaveLoadSystem system = Create(loadOnStart: true);
            int coins = 0;

            system.Register("coins", () => coins, value => coins = value); // before any frame: Start has not run

            Assert.AreEqual(77, coins, "the saved value is there before the first frame");
            Assert.AreEqual(3, system.Values.Get("level", 0), "values too");

            yield return null; // Start: the full load

            Assert.AreEqual(77, coins);
            Assert.AreEqual(0, system.CurrentSlot);
        }

        [UnityTest]
        public IEnumerator NoLoadOnStart_RegisteredBeforeStart_KeepsItsValue()
        {
            SaveLoadSystem system = Create(loadOnStart: false);
            int coins = 5;

            system.Register("coins", () => coins, value => coins = value);
            yield return null;

            Assert.AreEqual(5, coins, "nothing is loaded unless asked");
        }

        [UnityTest]
        public IEnumerator LoadOnStart_BrokenFile_EarlyReadStaysQuiet_TheLoadDealsWithIt()
        {
            _storage.Put(0, SaveFiles.SAVE, "{ not json");
            SaveLoadSystem system = Create(loadOnStart: true);
            int coins = 5;

            system.Register("coins", () => coins, value => coins = value);
            Assert.AreEqual(5, coins, "nothing read from a broken file");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("corrupted"));
            yield return null; // Start: the full load moves the file aside and says so, once

            Assert.AreEqual(5, coins);
        }
    }
}
