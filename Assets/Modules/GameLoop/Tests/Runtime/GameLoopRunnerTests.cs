using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ticking.Tests
{
    /// <summary>The runner on real frames: Unity's phases reach the loop, a pause stops them, death lets pauses go.</summary>
    public class GameLoopRunnerTests
    {
        private GameObject _runnerObject;
        private GameLoopRunner _runner;
        private GameObject _listenerObject;
        private TickCounter _counter;

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("Runner");
            _runner = _runnerObject.AddComponent<GameLoopRunner>();
            _listenerObject = new GameObject("Listener");
            _counter = _listenerObject.AddComponent<TickCounter>();
            _runner.Loop.Add(_counter);
        }

        [TearDown]
        public void TearDown()
        {
            if (_runnerObject != null)
                Object.DestroyImmediate(_runnerObject);
            Object.DestroyImmediate(_listenerObject);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Runner_Frames_TickEveryPhase()
        {
            yield return Frames(3);

            Assert.Greater(_counter.FixedUpdates, 0);
            Assert.Greater(_counter.Updates, 0);
            Assert.Greater(_counter.LateUpdates, 0);
            Assert.Greater(_runner.Loop.Time, 0f);
        }

        [UnityTest]
        public IEnumerator Runner_Paused_NothingTicks()
        {
            yield return Frames(1);
            _runner.Loop.Pause(this);
            int updates = _counter.Updates;
            int fixedUpdates = _counter.FixedUpdates;
            int lateUpdates = _counter.LateUpdates;
            float time = _runner.Loop.Time;

            yield return Frames(3);

            Assert.AreEqual(updates, _counter.Updates);
            Assert.AreEqual(fixedUpdates, _counter.FixedUpdates);
            Assert.AreEqual(lateUpdates, _counter.LateUpdates);
            Assert.AreEqual(time, _runner.Loop.Time);
            Assert.AreEqual(1f, Time.timeScale, "the pause does not touch timeScale");
        }

        [UnityTest]
        public IEnumerator Runner_Resumed_TicksAgain()
        {
            _runner.Loop.Pause(this);
            yield return Frames(1);
            _runner.Loop.Resume(this);
            int updates = _counter.Updates;

            yield return Frames(2);

            Assert.Greater(_counter.Updates, updates);
        }

        [Test]
        public void Runner_Destroyed_LetsEveryPauseGo()
        {
            GameLoop loop = _runner.Loop;
            loop.Pause(this);
            loop.Pause("menu");

            Object.DestroyImmediate(_runnerObject);

            Assert.IsFalse(loop.IsPaused);
        }

#if GAMELOOP_PHYSICS_2D
        [Test]
        public void Runner_DestroyedWhilePaused_GivesPhysicsModeBack()
        {
            SimulationMode2D mode = Physics2D.simulationMode;
            _runner.Loop.Pause(this);
            Assume.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));

            Object.DestroyImmediate(_runnerObject);

            Assert.AreEqual(mode, Physics2D.simulationMode);
        }

        [Test]
        public void Runner_Physics2DSwitchedOff_PauseKeepsPhysicsMode()
        {
            var go = new GameObject("Runner");
            go.SetActive(false);
            var runner = go.AddComponent<GameLoopRunner>();
            runner.Configure(physics2D: false, physics3D: false, animators: false, legacyAnimations: false,
                particles: false, audioListener: false);
            go.SetActive(true);
            SimulationMode2D mode = Physics2D.simulationMode;

            runner.Loop.Pause(this);
            SimulationMode2D paused = Physics2D.simulationMode;
            Object.DestroyImmediate(go);

            Assert.AreEqual(mode, paused);
        }
#endif
    }
}
