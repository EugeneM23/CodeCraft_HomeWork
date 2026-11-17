using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>
    /// Strength 1 holds full white for 1 s and fades over 3.5 s; strength 0.5 holds 0.65 s and fades
    /// over 2.25 s. The fade is quadratic: halfway through it, a quarter of the peak is left.
    /// </summary>
    public sealed class FlashScreenBehaviourTests
    {
        private GameObject _screen;
        private Image _image;
        private Event<float> _flashed;
        private IEntity _player;

        [SetUp]
        public void SetUp()
        {
            _screen = new GameObject("FlashScreen", typeof(RectTransform));
            _image = _screen.AddComponent<Image>();
            _image.color = Color.white;

            _flashed = new Event<float>();
            _player = new Entity();
            _player.AddFlashedEvent(_flashed);
            _player.AddBehaviour(new FlashScreenBehaviour(_image));
            _player.Enable();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_screen);

        private float Alpha => _image.color.a;

        [Test]
        public void AtStart_ScreenIsClear()
        {
            Assert.That(Alpha, Is.EqualTo(0f));
            Assert.That(_image.enabled, Is.False);
        }

        [Test]
        public void Flash_WhitesTheScreenOutAtItsStrength()
        {
            _flashed.Invoke(0.7f);

            Assert.That(Alpha, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(_image.enabled, Is.True);
        }

        [Test]
        public void StrongerThanFull_IsClampedToWhite()
        {
            _flashed.Invoke(3f);

            Assert.That(Alpha, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void DuringTheHold_ScreenStaysFullyWhite()
        {
            _flashed.Invoke(1f);

            _player.Tick(0.9f);

            Assert.That(Alpha, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void HalfwayThroughTheFade_AQuarterIsLeft()
        {
            _flashed.Invoke(1f);

            _player.Tick(1f + 1.75f);

            Assert.That(Alpha, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void AfterHoldAndFade_ScreenIsClearAndTheImageOff()
        {
            _flashed.Invoke(0.5f);

            _player.Tick(0.65f);
            _player.Tick(2.25f + 0.01f);

            Assert.That(Alpha, Is.EqualTo(0f));
            Assert.That(_image.enabled, Is.False);
        }

        [Test]
        public void WeakerFlashInTheGlare_IsLost()
        {
            _flashed.Invoke(1f);
            _player.Tick(1f + 1.75f); // a quarter left

            _flashed.Invoke(0.2f);
            _player.Tick(0f);

            Assert.That(Alpha, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void FlashStrongerThanWhatIsLeft_StartsOver()
        {
            _flashed.Invoke(1f);
            _player.Tick(1f + 1.75f); // a quarter left

            _flashed.Invoke(0.5f);
            _player.Tick(0.5f); // within the new 0.65 s hold

            Assert.That(Alpha, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void DisposedMidFlash_ScreenIsCleared_AndLaterFlashesIgnored()
        {
            _flashed.Invoke(1f);

            _player.Dispose();
            _flashed.Invoke(1f);

            Assert.That(Alpha, Is.EqualTo(0f));
            Assert.That(_image.enabled, Is.False);
        }
    }
}
