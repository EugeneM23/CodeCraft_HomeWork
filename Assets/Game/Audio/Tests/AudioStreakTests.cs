using NUnit.Framework;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioStreakTests
    {
        [Test]
        public void ALoneEvent_IsStepZero()
        {
            var streak = new AudioStreak(0.6f, 12);
            Assert.AreEqual(0, streak.Register(10f));
            Assert.AreEqual(0f, streak.Level);
        }

        [Test]
        public void EventsWithinTheWindow_StepUp()
        {
            var streak = new AudioStreak(0.6f, 12);
            streak.Register(1f);
            Assert.AreEqual(1, streak.Register(1.5f));
            Assert.AreEqual(2, streak.Register(2f));
            Assert.AreEqual(2f / 12f, streak.Level, 1e-5f);
        }

        [Test]
        public void AGapLongerThanTheWindow_StartsOver()
        {
            var streak = new AudioStreak(0.6f, 12);
            streak.Register(1f);
            streak.Register(1.2f);
            Assert.AreEqual(0, streak.Register(2f));
        }

        [Test]
        public void TheStreak_StopsAtMaxSteps()
        {
            var streak = new AudioStreak(1f, 3);
            for (int i = 0; i < 10; i++)
                streak.Register(i * 0.1f);
            Assert.AreEqual(3, streak.Steps);
            Assert.AreEqual(1f, streak.Level);
        }

        [Test]
        public void Reset_ForgetsTheStreak()
        {
            var streak = new AudioStreak(1f, 3);
            streak.Register(0f);
            streak.Register(0.1f);
            streak.Reset();
            Assert.AreEqual(0, streak.Register(0.2f));
        }

        [Test]
        public void RunAudio_WithoutALevel_CountsStreaksSilently()
        {
            var run = new RunAudio();
            run.XpPicked(0f);
            run.XpPicked(0.3f);
            run.Crushed(UnityEngine.Vector3.zero, 0f);
            run.Crushed(UnityEngine.Vector3.zero, 1f);
            Assert.AreEqual(1, run.XpStreak.Steps);
            Assert.AreEqual(1, run.CrushStreak.Steps);
        }
    }
}
