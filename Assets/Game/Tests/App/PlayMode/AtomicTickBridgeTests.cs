using System.Collections;
using Atomic.Entities;
using Game.GameLoop.Adapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class AtomicTickBridgeTests
    {
        private Ticking.GameLoop _loop;
        private AtomicTickBridge _bridge;
        private MonoEntity _entity;
        private TickCounterBehaviour _counter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _loop = new Ticking.GameLoop();
            _counter = new TickCounterBehaviour();
            _entity = MonoEntity.Create<MonoEntity>(name: "AtomicTickBridgeTests", behaviours: new IEntityBehaviour[] { _counter });
            yield return null; // Start: the entity joins Atomic's tick manager
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridge != null)
            {
                _loop.Remove(_bridge);
                _bridge.Dispose();
                _bridge = null;
            }

            if (_entity != null)
                Object.Destroy(_entity.gameObject);
        }

        [UnityTest]
        public IEnumerator WithoutTheBridge_TheEntityTicksItself()
        {
            int before = _counter.Ticks;
            yield return null;
            yield return null;

            Assert.Greater(_counter.Ticks, before);
        }

        [UnityTest]
        public IEnumerator WithTheBridge_TheEntityTicksOnlyFromTheLoop()
        {
            _bridge = new AtomicTickBridge();
            _loop.Add(_bridge);

            _loop.Update(0.016f);
            Assert.IsTrue(_bridge.IsConnected, "the bridge did not find Atomic's TickableManager");

            int afterLoopTick = _counter.Ticks;
            yield return null;
            yield return null;
            Assert.AreEqual(afterLoopTick, _counter.Ticks, "Unity still ticks Atomic under the bridge");

            _loop.Update(0.016f);
            Assert.AreEqual(afterLoopTick + 1, _counter.Ticks);
        }

        [UnityTest]
        public IEnumerator PausedLoop_StandsTheEntityStill()
        {
            _bridge = new AtomicTickBridge();
            _loop.Add(_bridge);
            _loop.Update(0.016f);
            int before = _counter.Ticks;

            _loop.Pause(this);
            _loop.Update(0.016f);
            _loop.LateUpdate(0.016f);
            yield return null;
            Assert.AreEqual(before, _counter.Ticks);

            _loop.Resume(this);
            _loop.Update(0.016f);
            Assert.AreEqual(before + 1, _counter.Ticks);
        }

        [UnityTest]
        public IEnumerator Disposed_TheEntityTicksItselfAgain()
        {
            _bridge = new AtomicTickBridge();
            _loop.Add(_bridge);
            _loop.Update(0.016f);

            _loop.Remove(_bridge);
            _bridge.Dispose();
            _bridge = null;

            int before = _counter.Ticks;
            yield return null;
            yield return null;
            Assert.Greater(_counter.Ticks, before);
        }

        private sealed class TickCounterBehaviour : IEntityTick
        {
            public int Ticks { get; private set; }

            public void Tick(IEntity entity, float deltaTime) => Ticks++;
        }
    }
}
