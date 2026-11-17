using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AudioSettingsUseCaseTests
    {
        [Test]
        public void Set_EachChannel_WritesOnlyItsOwnField()
        {
            AudioSettingsData audio = new();

            AudioSettingsUseCase.Set(audio, AudioChannel.Music, 0.3f);

            Assert.That(AudioSettingsUseCase.Get(audio, AudioChannel.Music), Is.EqualTo(0.3f));
            Assert.That(audio.music, Is.EqualTo(0.3f));
            Assert.That(audio.master, Is.EqualTo(1f));
            Assert.That(audio.effects, Is.EqualTo(1f));
        }

        [Test]
        public void Set_OutOfRange_IsClampedToZeroOne()
        {
            AudioSettingsData audio = new();

            AudioSettingsUseCase.Set(audio, AudioChannel.Ui, 1.7f);
            AudioSettingsUseCase.Set(audio, AudioChannel.Ambient, -0.5f);

            Assert.That(audio.ui, Is.EqualTo(1f));
            Assert.That(audio.ambient, Is.EqualTo(0f));
        }
    }
}
