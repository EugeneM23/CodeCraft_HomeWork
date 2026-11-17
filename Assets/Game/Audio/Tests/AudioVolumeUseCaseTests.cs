using NUnit.Framework;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioVolumeUseCaseTests
    {
        private const float EPSILON = 0.001f;

        [Test]
        public void ZeroVolume_IsMinus80Decibels() =>
            Assert.AreEqual(AudioVolumeUseCase.MIN_DECIBELS, AudioVolumeUseCase.ToDecibels(0f), EPSILON);

        [Test]
        public void FullVolume_IsZeroDecibels() =>
            Assert.AreEqual(0f, AudioVolumeUseCase.ToDecibels(1f), EPSILON);

        [Test]
        public void HalfVolume_IsAboutMinus6Decibels() =>
            Assert.AreEqual(-6.0206f, AudioVolumeUseCase.ToDecibels(0.5f), EPSILON);

        [Test]
        public void OutOfRangeVolume_IsClamped()
        {
            Assert.AreEqual(0f, AudioVolumeUseCase.ToDecibels(2f), EPSILON);
            Assert.AreEqual(AudioVolumeUseCase.MIN_DECIBELS, AudioVolumeUseCase.ToDecibels(-1f), EPSILON);
        }

        [Test]
        public void Minus80DecibelsAndBelow_IsSilence()
        {
            Assert.AreEqual(0f, AudioVolumeUseCase.ToLinear(-80f), EPSILON);
            Assert.AreEqual(0f, AudioVolumeUseCase.ToLinear(-120f), EPSILON);
        }

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.8f)]
        [TestCase(1f)]
        public void Volume_SurvivesTheRoundTripThroughDecibels(float linear) =>
            Assert.AreEqual(linear, AudioVolumeUseCase.ToLinear(AudioVolumeUseCase.ToDecibels(linear)), EPSILON);

        [TestCase(AudioChannel.Master, "MasterVolume")]
        [TestCase(AudioChannel.Music, "MusicVolume")]
        [TestCase(AudioChannel.Effects, "EffectsVolume")]
        [TestCase(AudioChannel.UI, "UIVolume")]
        [TestCase(AudioChannel.Ambient, "AmbientVolume")]
        public void Channel_MapsToItsExposedMixerParameter(AudioChannel channel, string parameter) =>
            Assert.AreEqual(parameter, AudioVolumeUseCase.ParameterOf(channel));
    }
}
