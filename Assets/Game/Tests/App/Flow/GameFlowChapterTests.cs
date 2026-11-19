using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>GameFlow plays levels as chapters of the game catalog: StartChapter and CurrentChapter.</summary>
    public class GameFlowChapterTests
    {
        private const string MENU = "Assets/Test/MainMenu.unity";
        private const string SCENE_A = "Assets/Test/A.unity";
        private const float DT = 0.1f;

        private static readonly ChapterId Outskirts = new("outskirts");
        private static readonly ChapterId Town = new("town");
        private static readonly ChapterId Quarry = new("quarry");

        private readonly List<Object> _objects = new();

        private LevelConfig _levelA;
        private LevelConfig _levelB;
        private FakeLevelLoader _loader;
        private GameFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _levelA = Make<LevelConfig>();
            _levelA.Configure("a", "A", 0, SCENE_A);
            _levelB = Make<LevelConfig>();
            _levelB.Configure("b", "B", 0, "Assets/Test/B.unity");
            var levels = Make<LevelCatalog>();
            levels.Configure(MENU, _levelA, _levelB);

            // Town is played on the same level as the Outskirts, after them: the Outskirts come first
            ChapterConfig outskirts = MakeChapter(Outskirts, _levelA);
            ChapterConfig town = MakeChapter(Town, _levelA);
            ChapterConfig quarry = MakeChapter(Quarry, null);
            var catalog = Make<GameCatalog>();
            catalog.Configure(new TankConfig[0], new ModuleConfig[0], new SkillConfig[0],
                new[] { outskirts, town, quarry });

            _loader = new FakeLevelLoader();
            _flow = new GameFlow(levels, _loader, gameCatalog: catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _objects)
                Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void CurrentChapter_IsNone_AtTheStart()
        {
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
            BootToMenu();
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
        }

        [Test]
        public void StartChapter_OpensTheChapterLevel_AndMakesItCurrentOnceLoaded()
        {
            BootToMenu();

            Assert.IsTrue(_flow.StartChapter(Town));
            Assert.AreSame(_levelA, _loader.LastLevel);
            Assert.IsTrue(_flow.CurrentChapter.IsNone, "The chapter is current only once its level is loaded");

            Finish();

            Assert.AreEqual(GameState.Level, _flow.State);
            Assert.AreSame(_levelA, _flow.CurrentLevel);
            Assert.AreEqual(Town, _flow.CurrentChapter);
        }

        [Test]
        public void StartChapter_ThroughTheMenuContract()
        {
            BootToMenu();
            IGameFlow menu = _flow;

            Assert.IsTrue(menu.StartChapter(Outskirts));
            Finish();

            Assert.AreEqual(Outskirts, menu.CurrentChapter);
        }

        [Test]
        public void StartChapter_Unknown_IsRefusedAndLogged()
        {
            BootToMenu();

            LogAssert.Expect(LogType.Error, new Regex("no chapter 'nope'"));
            Assert.IsFalse(_flow.StartChapter(new ChapterId("nope")));
            Assert.IsFalse(_flow.IsBusy);
        }

        [Test]
        public void StartChapter_WithoutALevel_IsRefusedAndLogged()
        {
            BootToMenu();

            LogAssert.Expect(LogType.Error, new Regex("'quarry' has no level"));
            Assert.IsFalse(_flow.StartChapter(Quarry));
            Assert.IsFalse(_flow.IsBusy);
        }

        [Test]
        public void StartChapter_WithoutAGameCatalog_IsRefusedAndLogged()
        {
            var levels = Make<LevelCatalog>();
            levels.Configure(MENU, _levelA);
            var flow = new GameFlow(levels, _loader);
            flow.Boot();
            _loader.Last.Complete();
            flow.Tick(DT);

            LogAssert.Expect(LogType.Error, new Regex("no chapter 'outskirts' in no game catalog"));
            Assert.IsFalse(flow.StartChapter(Outskirts));
        }

        [Test]
        public void StartChapter_BeforeTheMenu_IsRefused()
        {
            Assert.IsFalse(_flow.StartChapter(Outskirts));
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
        }

        [Test]
        public void RestartLevel_KeepsTheChapter()
        {
            BootToMenu();
            _flow.StartChapter(Town);
            Finish();

            Assert.IsTrue(_flow.RestartLevel());
            Assert.AreEqual(Town, _flow.CurrentChapter, "Kept while the restart loads");
            Finish();

            Assert.AreSame(_levelA, _flow.CurrentLevel);
            Assert.AreEqual(Town, _flow.CurrentChapter);
        }

        [Test]
        public void ReturnToMenu_ClearsTheChapter_AfterTheTransition()
        {
            BootToMenu();
            _flow.StartChapter(Outskirts);
            Finish();

            Assert.IsTrue(_flow.ReturnToMenu());
            Assert.AreEqual(Outskirts, _flow.CurrentChapter, "Still the chapter while the menu loads");
            Finish();

            Assert.AreEqual(GameState.MainMenu, _flow.State);
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
        }

        [Test]
        public void ALevelOpenedWithoutAChapter_HasNoChapter()
        {
            BootToMenu();
            _flow.StartChapter(Outskirts);
            Finish();

            Assert.IsTrue(_flow.StartLevel("b"));
            Finish();

            Assert.AreSame(_levelB, _flow.CurrentLevel);
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
        }

        [Test]
        public void AFailedTransition_KeepsTheChapter()
        {
            BootToMenu();
            _flow.StartChapter(Outskirts);
            Finish();

            _flow.StartChapter(Town);
            LogAssert.Expect(LogType.Error, new Regex("GameFlow: .*failed.*broken"));
            _loader.Last.FailWith("broken");
            _flow.Tick(DT);

            Assert.AreEqual(Outskirts, _flow.CurrentChapter);
        }

        [Test]
        public void EnterOpenScene_OfAChapterLevel_PlaysTheFirstChapterOnIt()
        {
            _flow.EnterOpenScene(SCENE_A);
            Assert.AreEqual(nameof(ILevelLoader.LoadMissing), _loader.LastCall);
            Finish();

            Assert.AreEqual(GameState.Level, _flow.State);
            Assert.AreEqual(Outskirts, _flow.CurrentChapter);
        }

        [Test]
        public void EnterOpenScene_OfALevelWithoutAChapter_HasNoChapter()
        {
            _flow.EnterOpenScene("Assets/Test/B.unity");
            Finish();

            Assert.AreSame(_levelB, _flow.CurrentLevel);
            Assert.IsTrue(_flow.CurrentChapter.IsNone);
        }

        private void BootToMenu()
        {
            _flow.Boot();
            Finish();
            Assert.AreEqual(GameState.MainMenu, _flow.State);
        }

        private void Finish()
        {
            _loader.Last.Complete();
            _flow.Tick(DT);
            Assert.IsFalse(_flow.IsBusy);
        }

        private ChapterConfig MakeChapter(ChapterId id, LevelConfig level)
        {
            var chapter = Make<ChapterConfig>();
            chapter.Configure(id, level, 1f, 1f, 1000, 10, null);
            return chapter;
        }

        private T Make<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _objects.Add(obj);
            return obj;
        }
    }
}
