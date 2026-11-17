using System.Collections;
using System.Linq;
using NUnit.Framework;
using Saving.Demo;
using Saving.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// The module's demo the way a beginner uses it: the SaveSystem and SaveSlotsMenu prefabs dropped into a scene,
    /// buttons pressed. Saves go to memory: the storage is swapped before the system starts.
    /// </summary>
    public class SaveSystemDemoTests
    {
        private static readonly string SCENE = TestScenes.MODULE + "/Demo/SaveSystemDemo.unity";

        private InMemorySaveStorage _storage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _storage = new InMemorySaveStorage();
            SceneManager.sceneLoaded += UseMemory;
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => scene.isLoaded);
            SceneManager.sceneLoaded -= UseMemory;
            yield return null;
#else
            Assert.Ignore("The demo scene loads only in the editor.");
            yield break;
#endif
        }

        // sceneLoaded comes after Awake and before Start: the prefab's system has not started yet
        private void UseMemory(Scene scene, LoadSceneMode mode) => SaveLoadSystem.Current.UseStorage(_storage);

        [TearDown]
        public void TearDown()
        {
            if (SaveLoadSystem.Current != null)
                Object.DestroyImmediate(SaveLoadSystem.Current.gameObject);
        }

        private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static void Press(string label) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(button => button.name == label).onClick.Invoke();

        private static IEnumerator LoadAndWait(string button)
        {
            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            SaveLoadSystem.Current.Loaded += OnLoaded;
            Press(button);
            yield return TestScenes.WaitFor(() => done);
            SaveLoadSystem.Current.Loaded -= OnLoaded;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Demo_PrefabsInPlace_SystemIsCurrentAndMenuClosed()
        {
            Assert.IsNotNull(SaveLoadSystem.Current);
            Assert.AreEqual("DontDestroyOnLoad", SaveLoadSystem.Current.gameObject.scene.name);
            Assert.IsFalse(Find<SaveSlotsMenu>().IsOpen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Demo_SaveChangeLoad_EverythingComesBack()
        {
            Press("+1 Coin");
            Press("+1 Coin");
            Press("Move Box");
            Press("Change Sign");
            var box = Find<DemoBox>();
            Vector3 position = box.transform.position;
            Color color = box.GetComponent<SpriteRenderer>().color;
            string colorName = box.LastColorName;
            string text = Find<DemoSign>().GetComponent<TextMeshPro>().text;
            Press("Save");

            Press("+1 Coin");
            for (int shuffle = 0; shuffle < 3 && box.transform.position == position; shuffle++)
                Press("Move Box");

            Find<DemoSign>().GetComponent<TextMeshPro>().text = "changed";
            yield return LoadAndWait("Load");

            box = Find<DemoBox>();
            Assert.AreEqual(2, Find<DemoCoins>().Coins);
            Assert.AreEqual(position, box.transform.position);
            Assert.AreEqual(color, box.GetComponent<SpriteRenderer>().color);
            Assert.AreEqual(1, box.Moves, "a script without [Save], saved by All Fields");
            Assert.AreEqual(colorName, box.LastColorName);
            Assert.AreEqual(text, Find<DemoSign>().GetComponent<TextMeshPro>().text, "TextMeshPro, Selected members");
        }

        [UnityTest]
        public IEnumerator Demo_AfterLoad_ButtonsStillSaveAndLoad()
        {
            Press("+1 Coin");
            Press("Save");
            // The load reloads the scene: its buttons now talk to the scene's own copy of the SaveSystem prefab
            yield return LoadAndWait("Load");
            Press("+1 Coin");
            Press("Save");
            Press("+1 Coin");
            yield return LoadAndWait("Load");

            Assert.AreEqual(2, Find<DemoCoins>().Coins);
        }

        [UnityTest]
        public IEnumerator Demo_SlotMenu_SaveAsThenLoadThatSlot()
        {
            Press("+1 Coin");
            Press("Save As…");
            var menu = Find<SaveSlotsMenu>();
            Assert.IsTrue(menu.IsOpen);
            menu.RowOf(2).PickButton.onClick.Invoke();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(_storage.Has(2, SaveFiles.SAVE));

            Press("+1 Coin");
            Press("Load…");
            Assert.IsTrue(menu.RowOf(2).PickButton.interactable);
            bool done = false;
            SaveLoadSystem.Current.Loaded += _ => done = true;
            menu.RowOf(2).PickButton.onClick.Invoke();
            yield return TestScenes.WaitFor(() => done);
            yield return null;

            Assert.AreEqual(2, SaveLoadSystem.Current.CurrentSlot);
            Assert.AreEqual(1, Find<DemoCoins>().Coins);
        }

        [UnityTest]
        public IEnumerator Demo_NewGameThenContinue()
        {
            Press("+1 Coin");
            Press("Save");
            DemoCoins before = Find<DemoCoins>();
            Press("New Game");
            yield return TestScenes.WaitFor(() => before == null && Find<DemoCoins>() != null);
            yield return null;

            Assert.AreEqual(1, SaveLoadSystem.Current.CurrentSlot, "slot 1 of the demo was free");
            Assert.AreEqual(0, Find<DemoCoins>().Coins);

            yield return LoadAndWait("Continue");
            Assert.AreEqual(0, SaveLoadSystem.Current.CurrentSlot, "the only save is slot 0");
            Assert.AreEqual(1, Find<DemoCoins>().Coins);
        }
    }
}
