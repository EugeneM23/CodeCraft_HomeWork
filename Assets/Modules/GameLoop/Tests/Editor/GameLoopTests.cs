using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ticking.Tests
{
    /// <summary>The loop core on plain C# listeners: phases, order, changes during a pass, errors.</summary>
    public class GameLoopTests
    {
        private GameLoop _loop;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _loop = new GameLoop();
            _log = new List<string>();
        }

        private Recorder Add(string name)
        {
            var recorder = new Recorder(name, _log);
            _loop.Add(recorder);
            return recorder;
        }

        private void TickAll(float deltaTime = 0.1f)
        {
            _loop.FixedUpdate(deltaTime);
            _loop.Update(deltaTime);
            _loop.LateUpdate(deltaTime);
        }

        // ---------- Phases ----------

        [Test]
        public void Update_Listener_GetsDeltaTime()
        {
            Recorder a = Add("a");

            _loop.Update(0.25f);

            Assert.AreEqual(new[] { "a.Update" }, _log);
            Assert.AreEqual(0.25f, a.LastDelta);
        }

        [Test]
        public void FixedUpdate_Listener_GetsDeltaTime()
        {
            Recorder a = Add("a");

            _loop.FixedUpdate(0.02f);

            Assert.AreEqual(new[] { "a.FixedUpdate" }, _log);
            Assert.AreEqual(0.02f, a.LastDelta);
        }

        [Test]
        public void LateUpdate_Listener_GetsDeltaTime()
        {
            Recorder a = Add("a");

            _loop.LateUpdate(0.5f);

            Assert.AreEqual(new[] { "a.LateUpdate" }, _log);
            Assert.AreEqual(0.5f, a.LastDelta);
        }

        [Test]
        public void Add_UpdateOnlyListener_OnlyItsPhaseTicks()
        {
            int updates = 0;
            _loop.Add(new UpdateOnly(() => updates++));

            TickAll();

            Assert.AreEqual(1, updates);
        }

        [Test]
        public void Add_SameListenerTwice_TicksOnce()
        {
            Recorder a = Add("a");
            _loop.Add(a);

            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "a.Update" }, _log);
        }

        [Test]
        public void Add_ObjectWithoutListenerInterfaces_Throws()
        {
            Assert.Throws<ArgumentException>(() => _loop.Add(new object()));
        }

        [Test]
        public void Add_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _loop.Add(null));
        }

        [Test]
        public void Remove_Listener_NoLongerTicks()
        {
            Recorder a = Add("a");

            _loop.Remove(a);
            TickAll();

            Assert.IsEmpty(_log);
        }

        [Test]
        public void Remove_UnknownOrNull_DoesNothing()
        {
            Add("a");

            Assert.DoesNotThrow(() => _loop.Remove(new Recorder("b", _log)));
            Assert.DoesNotThrow(() => _loop.Remove(null));
            _loop.Update(0.1f);
            Assert.AreEqual(new[] { "a.Update" }, _log);
        }

        [Test]
        public void Contains_FollowsAddAndRemove()
        {
            var a = new Recorder("a", _log);
            Assert.IsFalse(_loop.Contains(a));

            _loop.Add(a);
            Assert.IsTrue(_loop.Contains(a));

            _loop.Remove(a);
            Assert.IsFalse(_loop.Contains(a));
        }

        [Test]
        public void Counts_PerPhase()
        {
            Add("a");
            _loop.Add(new UpdateOnly(() => { }));

            Assert.AreEqual(2, _loop.ListenerCount);
            Assert.AreEqual(2, _loop.UpdateListenerCount);
            Assert.AreEqual(1, _loop.FixedUpdateListenerCount);
            Assert.AreEqual(1, _loop.LateUpdateListenerCount);
            Assert.AreEqual(1, _loop.PauseListenerCount);
        }

        // ---------- Order ----------

        [Test]
        public void Update_ByDefaultExecutionOrder()
        {
            _loop.Add(new Late(_log));
            _loop.Add(new Early(_log));
            Add("zero");

            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "early", "zero.Update", "late" }, _log);
        }

        [Test]
        public void Update_EqualOrder_InAddOrder()
        {
            Add("b");
            Add("a");
            Add("c");

            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "b.Update", "a.Update", "c.Update" }, _log);
        }

        [Test]
        public void Update_AddedAgain_GoesAfterItsOrderGroup()
        {
            Recorder a = Add("a");
            Add("b");
            _loop.Remove(a);
            _loop.Add(a);

            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "b.Update", "a.Update" }, _log);
        }

        // ---------- Changes during a pass ----------

        [Test]
        public void Update_AddedDuringPass_TicksFromNextPass()
        {
            var b = new Recorder("b", _log);
            Recorder a = Add("a");
            a.OnTick = () => _loop.Add(b);

            _loop.Update(0.1f);
            Assert.AreEqual(new[] { "a.Update" }, _log, "first pass");

            a.OnTick = null;
            _loop.Update(0.1f);
            Assert.AreEqual(new[] { "a.Update", "a.Update", "b.Update" }, _log);
        }

        [Test]
        public void Update_RemovedDuringPass_NotTickedInIt()
        {
            Recorder a = Add("a");
            Recorder b = Add("b");
            a.OnTick = () => _loop.Remove(b);

            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "a.Update" }, _log);
            Assert.IsFalse(_loop.Contains(b));
        }

        [Test]
        public void Update_RemovesItself_OthersStillTick()
        {
            Recorder a = Add("a");
            Add("b");
            a.OnTick = () => _loop.Remove(a);

            _loop.Update(0.1f);
            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "a.Update", "b.Update", "b.Update" }, _log);
        }

        [Test]
        public void Update_RemovedAndAddedAgainDuringPass_TicksOnceFromNextPass()
        {
            Recorder a = Add("a");
            Recorder b = Add("b");
            a.OnTick = () =>
            {
                _loop.Remove(b);
                _loop.Add(b);
            };

            _loop.Update(0.1f);
            Assert.AreEqual(new[] { "a.Update" }, _log, "first pass");

            a.OnTick = null;
            _loop.Update(0.1f);
            Assert.AreEqual(new[] { "a.Update", "a.Update", "b.Update" }, _log);
        }

        [Test]
        public void Update_AddedAndRemovedDuringPass_NeverTicks()
        {
            var b = new Recorder("b", _log);
            Recorder a = Add("a");
            a.OnTick = () =>
            {
                _loop.Add(b);
                _loop.Remove(b);
            };

            _loop.Update(0.1f);
            a.OnTick = null;
            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "a.Update", "a.Update" }, _log);
            Assert.IsFalse(_loop.Contains(b));
        }

        [Test]
        public void Update_AddedDuringPass_OtherPhasesTickItAtOnce()
        {
            var b = new Recorder("b", _log);
            Recorder a = Add("a");
            a.OnTick = () => _loop.Add(b);

            _loop.Update(0.1f);
            a.OnTick = null;
            _loop.LateUpdate(0.1f);

            Assert.AreEqual(new[] { "a.Update", "a.LateUpdate", "b.LateUpdate" }, _log);
        }

        // ---------- Errors ----------

        [Test]
        public void Update_ListenerThrows_OthersStillTick()
        {
            Recorder a = Add("a");
            Add("b");
            a.OnTick = () => throw new InvalidOperationException("boom");

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            _loop.Update(0.1f);

            Assert.AreEqual(new[] { "a.Update", "b.Update" }, _log);
        }

        // ---------- Time ----------

        [Test]
        public void Time_SumOfUpdateDeltas()
        {
            _loop.Update(0.25f);
            _loop.Update(0.5f);
            _loop.FixedUpdate(1f);
            _loop.LateUpdate(1f);

            Assert.AreEqual(0.75f, _loop.Time, 1e-6f);
        }

        [Test]
        public void Time_SeenByListenersIncludesThisFrame()
        {
            float seen = -1f;
            _loop.Add(new UpdateOnly(() => seen = _loop.Time));

            _loop.Update(0.25f);

            Assert.AreEqual(0.25f, seen, 1e-6f);
        }

        // ---------- Fakes ----------

        private sealed class Recorder : IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IPauseListener
        {
            private readonly string _name;
            private readonly List<string> _log;

            public Recorder(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public Action OnTick;
            public float LastDelta;

            public void OnUpdate(float deltaTime) => Record("Update", deltaTime);
            public void OnFixedUpdate(float deltaTime) => Record("FixedUpdate", deltaTime);
            public void OnLateUpdate(float deltaTime) => Record("LateUpdate", deltaTime);
            public void OnPause() => _log.Add($"{_name}.Pause");
            public void OnResume() => _log.Add($"{_name}.Resume");

            private void Record(string phase, float deltaTime)
            {
                _log.Add($"{_name}.{phase}");
                LastDelta = deltaTime;
                OnTick?.Invoke();
            }
        }

        private sealed class UpdateOnly : IUpdateListener
        {
            private readonly Action _onUpdate;

            public UpdateOnly(Action onUpdate) => _onUpdate = onUpdate;

            public void OnUpdate(float deltaTime) => _onUpdate();
        }

        [DefaultExecutionOrder(-5)]
        private sealed class Early : IUpdateListener
        {
            private readonly List<string> _log;

            public Early(List<string> log) => _log = log;

            public void OnUpdate(float deltaTime) => _log.Add("early");
        }

        [DefaultExecutionOrder(10)]
        private sealed class Late : IUpdateListener
        {
            private readonly List<string> _log;

            public Late(List<string> log) => _log = log;

            public void OnUpdate(float deltaTime) => _log.Add("late");
        }
    }
}
