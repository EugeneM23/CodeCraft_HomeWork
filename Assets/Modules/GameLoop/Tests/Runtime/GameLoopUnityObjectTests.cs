using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ticking.Tests
{
    /// <summary>The loop with Unity objects: switched off, inactive and destroyed listeners and pause sources.</summary>
    public class GameLoopUnityObjectTests
    {
        private GameLoop _loop;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _loop = new GameLoop();
            _go = new GameObject("Listener");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Loop_DisabledBehaviour_DoesNotTick()
        {
            var counter = _go.AddComponent<TickCounter>();
            _loop.Add(counter);

            counter.enabled = false;
            _loop.Update(0.1f);
            Assert.AreEqual(0, counter.Updates);

            counter.enabled = true;
            _loop.Update(0.1f);
            Assert.AreEqual(1, counter.Updates);
        }

        [Test]
        public void Loop_InactiveGameObject_DoesNotTick()
        {
            var counter = _go.AddComponent<TickCounter>();
            _loop.Add(counter);

            _go.SetActive(false);
            _loop.FixedUpdate(0.1f);
            _loop.Update(0.1f);
            _loop.LateUpdate(0.1f);

            Assert.AreEqual(0, counter.Updates + counter.FixedUpdates + counter.LateUpdates);
        }

        [Test]
        public void Loop_ListenerDestroyedWithoutLeaving_DropsOut()
        {
            var counter = _go.AddComponent<TickCounter>();
            _loop.Add(counter);

            Object.DestroyImmediate(counter);
            _loop.Update(0.1f);

            Assert.IsFalse(_loop.Contains(counter));
            Assert.AreEqual(0, _loop.UpdateListenerCount);
        }

        [Test]
        public void Loop_PauseSourceDestroyed_LetsPauseGo()
        {
            var changes = new List<bool>();
            _loop.PauseChanged += changes.Add;
            _loop.Pause(_go);

            Object.DestroyImmediate(_go);
            _loop.Update(0.1f);

            Assert.IsFalse(_loop.IsPaused);
            Assert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void Loop_PauseSourceDestroyed_OtherSourcesKeepIt()
        {
            _loop.Pause(_go);
            _loop.Pause("menu");

            Object.DestroyImmediate(_go);
            _loop.Update(0.1f);

            Assert.IsTrue(_loop.IsPaused);
            Assert.AreEqual(new object[] { "menu" }, _loop.PauseSources);
        }
    }
}
