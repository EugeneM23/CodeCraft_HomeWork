using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ticking.Tests
{
    /// <summary>Pause news changed from inside the news, a throwing PauseChanged subscriber, the listener set.</summary>
    public class GameLoopReentranceTests
    {
        private GameLoop _loop;
        private readonly object _menu = "menu";

        [SetUp]
        public void SetUp() => _loop = new GameLoop();

        [Test]
        public void PauseListener_ResumesInsideOnPause_LaterListenersNeverHearThePause()
        {
            var first = new PauseRecorder { OnPaused = () => _loop.Resume(_menu) };
            var second = new PauseRecorder();
            _loop.Add(first);
            _loop.Add(second);

            _loop.Pause(_menu);

            Assert.IsFalse(_loop.IsPaused);
            Assert.AreEqual(0, second.Pauses, "the pause was over before its news reached the second listener");
            Assert.AreEqual(1, second.Resumes);
        }

        [Test]
        public void PauseChanged_SubscriberThrows_LoggedAndPauseHolds()
        {
            var listener = new PauseRecorder();
            _loop.Add(listener);
            _loop.PauseChanged += _ => throw new InvalidOperationException("subscriber broke");

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: subscriber broke");
            _loop.Pause(_menu);

            Assert.IsTrue(_loop.IsPaused);
            Assert.AreEqual(1, listener.Pauses);
        }

        [Test]
        public void Listeners_EveryListenerOnce()
        {
            var a = new PauseRecorder();
            var b = new PauseRecorder();
            _loop.Add(a);
            _loop.Add(b);
            _loop.Add(a);

            CollectionAssert.AreEquivalent(new object[] { a, b }, _loop.Listeners);

            _loop.Remove(a);
            CollectionAssert.AreEquivalent(new object[] { b }, _loop.Listeners);
        }

        private sealed class PauseRecorder : IPauseListener
        {
            public int Pauses;
            public int Resumes;
            public Action OnPaused;

            public void OnPause()
            {
                Pauses++;
                OnPaused?.Invoke();
            }

            public void OnResume() => Resumes++;
        }
    }
}
