using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class LevelCatalogTests
    {
        private const string MENU = "Assets/Test/MainMenu.unity";
        private const string A = "Assets/Test/A.unity";
        private const string B_GEO = "Assets/Test/B_Geo.unity";
        private const string B = "Assets/Test/B.unity";

        private LevelCatalog _catalog;
        private LevelConfig _levelA;
        private LevelConfig _levelB;

        [SetUp]
        public void SetUp()
        {
            _levelA = ScriptableObject.CreateInstance<LevelConfig>();
            _levelA.Configure("a", "", 0, A);
            _levelB = ScriptableObject.CreateInstance<LevelConfig>();
            _levelB.Configure("b", "Level B", 1, B_GEO, B);
            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            _catalog.Configure(MENU, _levelA, _levelB);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_levelA);
            Object.DestroyImmediate(_levelB);
        }

        [Test]
        public void Find_ByIdOrNull()
        {
            Assert.AreSame(_levelB, _catalog.Find("b"));
            Assert.IsNull(_catalog.Find("c"));
            Assert.IsNull(_catalog.Find(null));
        }

        [Test]
        public void FindByScene_FindsTheLevelOfAnyOfItsScenes()
        {
            Assert.AreSame(_levelB, _catalog.FindByScene(B_GEO));
            Assert.AreSame(_levelB, _catalog.FindByScene(B));
            Assert.AreSame(_levelA, _catalog.FindByScene(A));
            Assert.IsNull(_catalog.FindByScene(MENU));
            Assert.IsNull(_catalog.FindByScene("Assets/Test/Other.unity"));
            Assert.IsNull(_catalog.FindByScene(""));
        }

        [Test]
        public void IsMenuScene_OnlyForTheMenuPath()
        {
            Assert.IsTrue(_catalog.IsMenuScene(MENU));
            Assert.IsFalse(_catalog.IsMenuScene(A));
            Assert.IsFalse(_catalog.IsMenuScene(null));
        }

        [Test]
        public void FirstLevel_IsTheFirstOfTheList_OrNullWhenEmpty()
        {
            Assert.AreSame(_levelA, _catalog.FirstLevel);

            _catalog.Configure(MENU);
            Assert.IsNull(_catalog.FirstLevel);
        }

        [Test]
        public void LevelConfig_ActiveAndGameplayScenes()
        {
            Assert.AreEqual(B, _levelB.ActiveScenePath);
            Assert.AreEqual(1, _levelB.GameplaySceneIndex, "The gameplay scene is the active one unless set");
            Assert.AreEqual(B, _levelB.GameplayScenePath);

            _levelB.Configure(0, _levelA);
            Assert.AreEqual(B_GEO, _levelB.GameplayScenePath);
            Assert.AreSame(_levelA, _levelB.RequiredLevel);
        }

        [Test]
        public void LevelConfig_DisplayNameFallsBackToTheId()
        {
            Assert.AreEqual("a", _levelA.DisplayName);
            Assert.AreEqual("Level B", _levelB.DisplayName);
        }

        [Test]
        public void LevelConfig_ActiveIndexOutOfTheScenes_HasNoActivePath()
        {
            _levelA.Configure("a", "", 3, A);
            Assert.AreEqual(string.Empty, _levelA.ActiveScenePath);
        }
    }
}
