using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScreenNavigation.Tests
{
    /// <summary>FadeTransition: alpha over Duration of unscaled time, interruption, transparent when switched off.</summary>
    public class FadeTransitionTests
    {
        private GameObject _go;
        private FadeTransition _fade;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Fade", typeof(RectTransform));
            _fade = _go.AddComponent<FadeTransition>();
            _group = _go.GetComponent<CanvasGroup>();
            _fade.Configure(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Time.timeScale = 1f;
        }

        [Test]
        public void FadeTransition_Ticks_ReachesTargetAfterDuration()
        {
            bool done = false;

            _fade.Play(true, () => done = true);
            _fade.Tick(0.1f);

            Assert.AreEqual(0.5f, _group.alpha, 1e-4f);
            Assert.IsFalse(done);
            Assert.IsTrue(_fade.IsPlaying);

            _fade.Tick(0.1f);

            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(done);
            Assert.IsFalse(_fade.IsPlaying);
        }

        [Test]
        public void FadeTransition_ZeroDuration_FinishesAtOnce()
        {
            _fade.Configure(0f);
            bool done = false;

            _fade.Play(true, () => done = true);

            Assert.IsTrue(done);
            Assert.AreEqual(1f, _group.alpha);
        }

        [Test]
        public void FadeTransition_Interrupted_DropsOldCallback()
        {
            bool shown = false;
            bool hidden = false;

            _fade.Play(true, () => shown = true);
            _fade.Tick(0.1f);
            _fade.Play(false, () => hidden = true);
            _fade.Tick(1f);

            Assert.IsFalse(shown);
            Assert.IsTrue(hidden);
            Assert.AreEqual(0f, _group.alpha);
        }

        [Test]
        public void FadeTransition_SwitchedOff_BecomesTransparent()
        {
            _fade.Play(true, null);
            _fade.Tick(1f);

            _go.SetActive(false);

            Assert.AreEqual(0f, _group.alpha, "the next show fades in from zero");
        }

        [UnityTest]
        public IEnumerator FadeTransition_GamePaused_FinishesOnRealTime()
        {
            Time.timeScale = 0f;
            bool done = false;

            _fade.Play(true, () => done = true);
            float until = Time.realtimeSinceStartup + 1f;
            while (!done && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.IsTrue(done);
        }
    }
}
