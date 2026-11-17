using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class GameFlowTests
    {
        private const string MENU = "Assets/Test/MainMenu.unity";
        private const float DT = 0.1f;

        private LevelCatalog _catalog;
        private LevelConfig _levelA;
        private LevelConfig _levelB;
        private FakeLevelLoader _loader;
        private FakeLoadingScreen _screen;
        private Ticking.GameLoop _loop;
        private GameFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _levelA = ScriptableObject.CreateInstance<LevelConfig>();
            _levelA.Configure("a", "A", 0, "Assets/Test/A.unity");
            _levelB = ScriptableObject.CreateInstance<LevelConfig>();
            _levelB.Configure("b", "B", 1, "Assets/Test/B_Geo.unity", "Assets/Test/B.unity");
            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            _catalog.Configure(MENU, _levelA, _levelB);

            _loader = new FakeLevelLoader();
            _screen = new FakeLoadingScreen();
            _loop = new Ticking.GameLoop();
            _flow = new GameFlow(_catalog, _loader, _screen, _loop);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_levelA);
            Object.DestroyImmediate(_levelB);
        }

        [Test]
        public void Boot_LoadsTheMenuBehindAnInstantScreen_AndEntersMainMenuWhenLoaded()
        {
            var states = new List<GameState>();
            _flow.StateChanged += states.Add;

            Assert.IsTrue(_flow.Boot());
            Assert.IsTrue(_flow.IsBusy);
            Assert.AreEqual(GameState.Starting, _flow.State);
            Assert.AreEqual(MENU, _loader.LastScene);
            Assert.IsTrue(_screen.IsShown);
            Assert.IsTrue(_screen.LastInstant);

            _loader.Last.Complete();
            _flow.Tick(DT);

            Assert.IsFalse(_flow.IsBusy);
            Assert.AreEqual(GameState.MainMenu, _flow.State);
            CollectionAssert.AreEqual(new[] { GameState.MainMenu }, states);
            Assert.IsFalse(_screen.IsShown);
            Assert.AreEqual(1, _screen.HideCount);
        }

        [Test]
        public void Boot_Twice_IsRefused()
        {
            Assert.IsTrue(_flow.Boot());
            Assert.IsFalse(_flow.Boot());

            Finish();
            Assert.IsFalse(_flow.Boot());
        }

        [Test]
        public void OpenLevel_FromTheMenu_LoadsTheLevel_AndEntersIt()
        {
            BootToMenu();
            var transitions = new List<(GameState, GameState)>();
            _flow.TransitionStarted += (from, to) => transitions.Add((from, to));

            Assert.IsTrue(_flow.OpenLevel(_levelB));
            Assert.AreEqual(nameof(ILevelLoader.Load), _loader.LastCall);
            Assert.AreSame(_levelB, _loader.LastLevel);
            CollectionAssert.AreEqual(new[] { (GameState.MainMenu, GameState.Level) }, transitions);
            Assert.IsNull(_flow.CurrentLevel, "The level is current only once loaded");

            Finish();

            Assert.AreEqual(GameState.Level, _flow.State);
            Assert.AreSame(_levelB, _flow.CurrentLevel);
            Assert.AreEqual("b", _flow.CurrentLevelId);
        }

        [Test]
        public void StartLevel_FindsTheLevelByIdInTheCatalog()
        {
            BootToMenu();

            Assert.IsTrue(_flow.StartLevel("a"));
            Assert.AreSame(_levelA, _loader.LastLevel);
        }

        [Test]
        public void StartLevel_UnknownId_IsRefused()
        {
            BootToMenu();

            LogAssert.Expect(LogType.Error, new Regex("no level 'nope'"));
            Assert.IsFalse(_flow.StartLevel("nope"));
            Assert.IsFalse(_flow.IsBusy);
        }

        [Test]
        public void Requests_WhileATransitionRuns_AreRefused()
        {
            BootToMenu();
            Assert.IsTrue(_flow.OpenLevel(_levelA));

            Assert.IsFalse(_flow.OpenLevel(_levelB));
            Assert.IsFalse(_flow.StartLevel("b"));
            Assert.IsFalse(_flow.ReturnToMenu());
            Assert.AreSame(_levelA, _loader.LastLevel);
        }

        [Test]
        public void Requests_ThatMakeNoSenseInTheState_AreRefused()
        {
            Assert.IsFalse(_flow.OpenLevel(_levelA), "Starting: no level before the menu");
            Assert.IsFalse(_flow.ReturnToMenu());
            Assert.IsFalse(_flow.RestartLevel());

            BootToMenu();
            Assert.IsFalse(_flow.ReturnToMenu(), "Already in the menu");
            Assert.IsFalse(_flow.RestartLevel(), "No level to restart");
        }

        [Test]
        public void RestartLevel_LoadsTheCurrentLevelAgain()
        {
            BootToMenu();
            _flow.OpenLevel(_levelA);
            Finish();

            Assert.IsTrue(_flow.RestartLevel());
            Assert.AreSame(_levelA, _loader.LastLevel);
            Finish();

            Assert.AreEqual(GameState.Level, _flow.State);
            Assert.AreSame(_levelA, _flow.CurrentLevel);
        }

        [Test]
        public void ReturnToMenu_KeepsTheRequestedScreen_UntilTheNextTransition()
        {
            BootToMenu();
            _flow.OpenLevel(_levelA);
            Finish();

            Assert.IsTrue(_flow.ReturnToMenu("Hangar"));
            Assert.AreEqual(MENU, _loader.LastScene);
            Finish();

            Assert.AreEqual(GameState.MainMenu, _flow.State);
            Assert.AreEqual("Hangar", _flow.RequestedMenuScreen);
            Assert.IsNull(_flow.CurrentLevel);
            Assert.IsNull(_flow.CurrentLevelId);

            _flow.OpenLevel(_levelA);
            Assert.IsNull(_flow.RequestedMenuScreen);
        }

        [Test]
        public void FailedTransition_IsLogged_KeepsTheState_AndHidesTheScreen()
        {
            BootToMenu();
            bool? finished = null;
            _flow.LoadingFinished += succeeded => finished = succeeded;

            _flow.OpenLevel(_levelA);
            LogAssert.Expect(LogType.Error, new Regex("GameFlow: .*failed.*broken scene"));
            _loader.Last.FailWith("broken scene");
            _flow.Tick(DT);

            Assert.IsFalse(_flow.IsBusy);
            Assert.AreEqual(GameState.MainMenu, _flow.State);
            Assert.IsNull(_flow.CurrentLevel);
            Assert.IsFalse(_screen.IsShown);
            Assert.AreEqual(false, finished);
        }

        [Test]
        public void ATreeThatCannotBeBuilt_IsRefused_WithoutStartingATransition()
        {
            BootToMenu();
            int started = 0;
            _flow.LoadingStarted += () => started++;
            _loader.ThrowOnBuild = new ArgumentException("no scenes");

            LogAssert.Expect(LogType.Error, new Regex("cannot start.*no scenes"));
            Assert.IsFalse(_flow.OpenLevel(_levelA));
            Assert.IsFalse(_flow.IsBusy);
            Assert.AreEqual(0, started);
        }

        [Test]
        public void TheScenesChange_OnlyOnceTheScreenCovers()
        {
            BootToMenu();
            _screen.CoverAtOnce = false;

            _flow.OpenLevel(_levelA);
            ManualOperation load = _loader.Last;
            _flow.Tick(DT);
            Assert.IsFalse(load.Begun, "The screen is still fading in");

            _screen.Covered = true;
            _flow.Tick(DT);
            Assert.IsTrue(load.Begun);
        }

        [Test]
        public void AScreenThatNeverCovers_DoesNotHoldTheGame()
        {
            BootToMenu();
            _screen.CoverAtOnce = false;

            _flow.OpenLevel(_levelA);
            for (int i = 0; i < 100 && !_loader.Last.Begun; i++)
                _flow.Tick(DT);

            Assert.IsTrue(_loader.Last.Begun);
        }

        [Test]
        public void Progress_GoesToTheScreen()
        {
            BootToMenu();
            _flow.OpenLevel(_levelA);

            _loader.Last.SetProgress(0.5f);
            _flow.Tick(DT);

            Assert.AreEqual(0.5f, _screen.LastProgress, 1e-4f);
            Assert.AreEqual(0.5f, _flow.LoadingProgress, 1e-4f);

            Finish();
            Assert.AreEqual(1f, _screen.LastProgress);
            Assert.AreEqual(0f, _flow.LoadingProgress);
        }

        [Test]
        public void ATransition_LetsEveryPauseGo()
        {
            BootToMenu();
            _flow.OpenLevel(_levelA);
            _loop.Pause(this);
            _loop.Pause(_screen);

            Finish();

            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Events_ComeInOrder()
        {
            var log = new List<string>();
            _flow.TransitionStarted += (from, to) => log.Add($"transition {from}>{to}");
            _flow.LoadingStarted += () => log.Add("loading");
            _flow.StateChanged += state => log.Add($"state {state}");
            _flow.LoadingFinished += ok => log.Add($"finished {ok}");

            _flow.Boot();
            Finish();

            CollectionAssert.AreEqual(new[]
            {
                "transition Starting>MainMenu", "loading", "finished True", "state MainMenu"
            }, log);
        }

        [Test]
        public void AThrowingSubscriber_DoesNotStopTheFlow()
        {
            _flow.TransitionStarted += (_, _) => throw new InvalidOperationException("autosave broke");

            LogAssert.Expect(LogType.Exception, new Regex("autosave broke"));
            Assert.IsTrue(_flow.Boot());
            Finish();

            Assert.AreEqual(GameState.MainMenu, _flow.State);
        }

        [Test]
        public void WithoutAScreen_TransitionsRunUncovered()
        {
            var flow = new GameFlow(_catalog, _loader);

            Assert.IsTrue(flow.Boot());
            Assert.IsTrue(_loader.Last.Begun);
            _loader.Last.Complete();
            flow.Tick(DT);

            Assert.AreEqual(GameState.MainMenu, flow.State);
        }

        private void BootToMenu()
        {
            _flow.Boot();
            Finish();
            Assert.AreEqual(GameState.MainMenu, _flow.State);
        }

        [Test]
        public void ALevelOpenedFromStateChanged_KeepsItsLevel()
        {
            _flow.Boot();
            _flow.StateChanged += state =>
            {
                if (state == GameState.MainMenu)
                    _flow.OpenLevel(_levelA);
            };

            _loader.Last.Complete();
            _flow.Tick(DT);

            Assert.IsTrue(_flow.IsBusy, "the level transition started from the handler runs");
            Assert.AreSame(_levelA, _loader.LastLevel);
            Finish();
            Assert.AreEqual(GameState.Level, _flow.State);
            Assert.AreEqual("a", _flow.CurrentLevelId);
        }

        private void Finish()
        {
            _loader.Last.Complete();
            _flow.Tick(DT);
            Assert.IsFalse(_flow.IsBusy);
        }
    }
}
