using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Ticking.Tests
{
    /// <summary>Pause with sources: what stops, who holds it, who hears about it.</summary>
    public class GameLoopPauseTests
    {
        private GameLoop _loop;
        private Listener _listener;
        private readonly object _menu = "menu";
        private readonly object _loader = "loader";

        [SetUp]
        public void SetUp()
        {
            _loop = new GameLoop();
            _listener = new Listener();
            _loop.Add(_listener);
        }

        private void TickAll(float deltaTime = 0.1f)
        {
            _loop.FixedUpdate(deltaTime);
            _loop.Update(deltaTime);
            _loop.LateUpdate(deltaTime);
        }

        [Test]
        public void Pause_NoPhaseTicks()
        {
            _loop.Pause(_menu);

            TickAll();

            Assert.AreEqual(0, _listener.Ticks);
            Assert.IsTrue(_loop.IsPaused);
        }

        [Test]
        public void Resume_TicksAgain()
        {
            _loop.Pause(_menu);
            _loop.Resume(_menu);

            TickAll();

            Assert.AreEqual(3, _listener.Ticks);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Pause_TwoSources_RunsOnlyWhenBothLetGo()
        {
            _loop.Pause(_menu);
            _loop.Pause(_loader);

            _loop.Resume(_menu);
            Assert.IsTrue(_loop.IsPaused, "the loader still holds it");

            _loop.Resume(_loader);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Pause_SameSourceTwice_OneResumeLetsGo()
        {
            _loop.Pause(_menu);
            _loop.Pause(_menu);

            _loop.Resume(_menu);

            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Resume_SourceWithoutPause_KeepsOthers()
        {
            _loop.Pause(_menu);

            _loop.Resume(_loader);
            _loop.Resume(null);

            Assert.IsTrue(_loop.IsPaused);
        }

        [Test]
        public void Pause_NullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _loop.Pause(null));
        }

        [Test]
        public void IsPausedBy_AndSources_InOrder()
        {
            _loop.Pause(_loader);
            _loop.Pause(_menu);

            Assert.IsTrue(_loop.IsPausedBy(_menu));
            Assert.IsFalse(_loop.IsPausedBy("other"));
            Assert.AreEqual(new[] { _loader, _menu }, _loop.PauseSources);
        }

        [Test]
        public void ResumeAll_LetsEverySourceGo()
        {
            _loop.Pause(_menu);
            _loop.Pause(_loader);

            _loop.ResumeAll();

            Assert.IsFalse(_loop.IsPaused);
            Assert.IsEmpty(_loop.PauseSources);
            Assert.AreEqual(1, _listener.Resumes);
        }

        [Test]
        public void PauseChanged_OnlyOnTransitions()
        {
            var changes = new List<bool>();
            _loop.PauseChanged += changes.Add;

            _loop.Pause(_menu);
            _loop.Pause(_loader);
            _loop.Resume(_menu);
            _loop.Resume(_loader);

            Assert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void PauseListener_HearsTransitions()
        {
            _loop.Pause(_menu);
            _loop.Pause(_loader);
            Assert.AreEqual(1, _listener.Pauses);
            Assert.AreEqual(0, _listener.Resumes);

            _loop.Resume(_menu);
            _loop.Resume(_loader);
            Assert.AreEqual(1, _listener.Resumes);
        }

        [Test]
        public void PauseListener_AddedWhilePaused_GetsOnPauseAtOnce()
        {
            _loop.Pause(_menu);
            var late = new Listener();

            _loop.Add(late);

            Assert.AreEqual(1, late.Pauses);
            _loop.Resume(_menu);
            Assert.AreEqual(1, late.Resumes);
        }

        [Test]
        public void PauseListener_RemovedWhilePaused_NotToldAboutResume()
        {
            _loop.Pause(_menu);
            _loop.Remove(_listener);

            _loop.Resume(_menu);

            Assert.AreEqual(0, _listener.Resumes);
        }

        [Test]
        public void PauseListener_ResumesInsideOnPause_GameRunsAndLastNewsIsResume()
        {
            var changes = new List<bool>();
            _loop.PauseChanged += changes.Add;
            _listener.OnPaused = () => _loop.Resume(_menu);

            _loop.Pause(_menu);

            Assert.IsFalse(_loop.IsPaused);
            Assert.AreEqual(false, changes[changes.Count - 1]);
            Assert.AreEqual(1, _listener.Resumes);
        }

        [Test]
        public void PauseListener_Throws_OthersStillHear()
        {
            var second = new Listener();
            _loop.Add(second);
            _listener.OnPaused = () => throw new InvalidOperationException("boom");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, "InvalidOperationException: boom");
            _loop.Pause(_menu);

            Assert.AreEqual(1, second.Pauses);
            Assert.IsTrue(_loop.IsPaused);
        }

        [Test]
        public void Time_StandsWhilePaused()
        {
            _loop.Update(0.5f);
            _loop.Pause(_menu);
            _loop.Update(1f);
            _loop.Resume(_menu);
            _loop.Update(0.25f);

            Assert.AreEqual(0.75f, _loop.Time, 1e-6f);
        }

        [Test]
        public void PausedDuringPass_RestOfPassTicks_NextPassStops()
        {
            var second = new Listener();
            _loop.Add(second);
            _listener.OnTicked = () => _loop.Pause(_menu);

            _loop.Update(0.1f);
            Assert.AreEqual(1, second.Ticks, "the pass that paused finishes");

            _loop.Update(0.1f);
            Assert.AreEqual(1, second.Ticks);
        }

        private sealed class Listener : IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IPauseListener
        {
            public int Ticks;
            public int Pauses;
            public int Resumes;
            public Action OnPaused;
            public Action OnTicked;

            public void OnUpdate(float deltaTime) => Tick();
            public void OnFixedUpdate(float deltaTime) => Tick();
            public void OnLateUpdate(float deltaTime) => Tick();

            public void OnPause()
            {
                Pauses++;
                OnPaused?.Invoke();
            }

            public void OnResume() => Resumes++;

            private void Tick()
            {
                Ticks++;
                OnTicked?.Invoke();
            }
        }
    }
}
