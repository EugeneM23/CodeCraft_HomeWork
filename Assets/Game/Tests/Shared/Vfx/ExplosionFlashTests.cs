using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The flash's life, driven by hand: in edit mode Awake, OnEnable and Update do not run on
    /// their own. Defaults: 6 at full, gone after 0.12 s.
    /// </summary>
    public sealed class ExplosionFlashTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _effect;
        private ExplosionFlash _flash;
        private Light _light;

        [SetUp]
        public void SetUp()
        {
            _effect = new GameObject("Explosion");
            _flash = _effect.AddComponent<ExplosionFlash>();
            _light = _effect.GetComponent<Light>();
            _light.shadows = LightShadows.Soft;
            Call("Awake");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_effect);

        [Test]
        public void Flash_CastsNoShadows()
        {
            Assert.That(_light.shadows, Is.EqualTo(LightShadows.None));
        }

        [Test]
        public void Spawn_LightsUpAtFullIntensity()
        {
            _light.intensity = 0f;
            _light.enabled = false;

            Call("OnEnable");

            Assert.That(_light.enabled, Is.True);
            Assert.That(_light.intensity, Is.EqualTo(6f));
        }

        [Test]
        public void WhileFading_DimsWithTheSquareOfWhatIsLeft()
        {
            Call("OnEnable");
            SetTime(0.03f);

            Call("Update");

            // Update adds whatever this editor frame's delta time is, so read back where it got to.
            float left = 1f - GetTime() / 0.12f;
            Assume.That(left, Is.GreaterThan(0f));
            Assert.That(_light.intensity, Is.EqualTo(6f * left * left).Within(1e-3f));
            Assert.That(_light.intensity, Is.LessThan(6f * 0.75f * 0.75f + 1e-3f));
        }

        [Test]
        public void OnceFaded_TheLightIsSwitchedOff()
        {
            Call("OnEnable");
            SetTime(0.12f);

            Call("Update");

            Assert.That(_light.enabled, Is.False);
        }

        [Test]
        public void SpawnedAgainFromThePool_FlashesAgain()
        {
            Call("OnEnable");
            SetTime(0.12f);
            Call("Update");

            Call("OnEnable");

            Assert.That(_light.enabled, Is.True);
            Assert.That(_light.intensity, Is.EqualTo(6f));
        }

        [Test]
        public void SwitchedOffLight_StaysAsItIs()
        {
            Call("OnEnable");
            _light.enabled = false;
            _light.intensity = 2f;

            Call("Update");

            Assert.That(_light.enabled, Is.False);
            Assert.That(_light.intensity, Is.EqualTo(2f));
        }

        private void Call(string method) => typeof(ExplosionFlash).GetMethod(method, Private).Invoke(_flash, null);

        private float GetTime() => (float)typeof(ExplosionFlash).GetField("_time", Private).GetValue(_flash);

        private void SetTime(float seconds) => typeof(ExplosionFlash).GetField("_time", Private).SetValue(_flash, seconds);
    }
}
