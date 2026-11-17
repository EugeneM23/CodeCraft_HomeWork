using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CooldownTimerTests
    {
        [Test]
        public void NewTimer_StartsExpired_ByDefault()
        {
            Assert.That(new CooldownTimer(2f).IsExpired(), Is.True);
        }

        [Test]
        public void Reset_StartsAFullCooldown_ThatRunsDownWithTicks()
        {
            var timer = new CooldownTimer(2f);

            timer.Reset();
            timer.Tick(0.5f);

            Assert.That(timer.IsExpired(), Is.False);
            Assert.That(timer.GetProgress(), Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Tick_PastTheEnd_StopsAtZero()
        {
            var timer = new CooldownTimer(1f, 1f);

            timer.Tick(5f);

            Assert.That(timer.IsExpired(), Is.True);
            Assert.That(timer.GetProgress(), Is.EqualTo(0f));
        }

        [Test]
        public void SetDuration_AppliesFromTheNextReset()
        {
            var timer = new CooldownTimer(1f);

            timer.SetDuration(4f);
            timer.Reset();
            timer.Tick(3f);

            Assert.That(timer.Duration, Is.EqualTo(4f));
            Assert.That(timer.IsExpired(), Is.False);
        }
    }
}
