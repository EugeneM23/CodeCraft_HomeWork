using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Loading.Tests
{
    public class FadeOperationTests
    {
        private GameObject _go;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Fade", typeof(CanvasGroup));
            _group = _go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Fade_Tick_MovesAlphaOverDuration()
        {
            var fade = new FadeOperation(_group, 1f, 0.5f);

            fade.Begin();
            Assert.AreEqual(0f, _group.alpha);

            fade.Tick(0.25f);
            Assert.AreEqual(0.5f, _group.alpha, 1e-4f);
            Assert.AreEqual(LoadingStatus.Running, fade.Status);

            fade.Tick(0.25f);
            Assert.AreEqual(1f, _group.alpha);
            Assert.AreEqual(LoadingStatus.Succeeded, fade.Status);
        }

        [Test]
        public void Fade_Out_LowersAlpha()
        {
            _group.alpha = 1f;
            var fade = new FadeOperation(_group, 0f, 1f);
            fade.Begin();

            fade.Tick(2f);

            Assert.AreEqual(0f, _group.alpha);
            Assert.AreEqual(LoadingStatus.Succeeded, fade.Status);
        }

        [Test]
        public void Fade_ZeroDuration_SetsTargetAtOnce()
        {
            var fade = new FadeOperation(_group, 1f, 0f);

            fade.Begin();

            Assert.AreEqual(1f, _group.alpha);
            Assert.AreEqual(LoadingStatus.Succeeded, fade.Status);
        }

        [Test]
        public void Fade_AlreadyAtTarget_SucceedsAtOnce()
        {
            var fade = new FadeOperation(_group, 0f, 1f);

            fade.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, fade.Status);
        }

        [Test]
        public void Fade_IsWeightlessByDefault()
        {
            Assert.AreEqual(0f, new FadeOperation(_group, 1f, 1f).Weight);
        }

        [Test]
        public void Fade_NullGroup_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FadeOperation(null, 1f, 1f));
        }

        [Test]
        public void Fade_GroupDestroyed_Fails()
        {
            var fade = new FadeOperation(_group, 1f, 1f);
            fade.Begin();

            Object.DestroyImmediate(_go);
            fade.Tick(0.1f);

            Assert.AreEqual(LoadingStatus.Failed, fade.Status);
        }
    }
}
