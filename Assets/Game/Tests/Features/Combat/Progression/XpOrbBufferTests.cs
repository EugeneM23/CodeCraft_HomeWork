using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class XpOrbBufferTests
    {
        private const float DT = 1f / 60f;

        private static XpOrbBuffer Create(int capacity = 8) => new(capacity, 20f, 0.6f, 6f, 25f, 40f);

        private static void Run(XpOrbBuffer buffer, float seconds, Vector3 target, bool hasTarget, float radius,
            out int collectedValue)
        {
            collectedValue = 0;
            for (float t = 0f; t < seconds; t += DT)
            {
                buffer.Step(DT, target, hasTarget, radius);
                for (int i = 0; i < buffer.CollectedCount; i++)
                {
                    buffer.GetCollected(i, out _, out int value);
                    collectedValue += value;
                }
            }
        }

        [Test]
        public void Dropped_HopsAndLandsOnItsGround()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(0f, 1f, 0f), new Vector3(1f, 4f, 0f), 0.5f, 3);
            Assert.AreEqual(XpOrbBuffer.OrbState.Falling, buffer.GetState(0));

            Run(buffer, 2f, Vector3.zero, false, 4f, out _);

            Assert.AreEqual(XpOrbBuffer.OrbState.Lying, buffer.GetState(0));
            Assert.AreEqual(0.5f, buffer.GetPosition(0).y, 1e-5f);
            Assert.Greater(buffer.GetPosition(0).x, 0f);
        }

        [Test]
        public void Full_MergesIntoTheNearestLyingOrb_NoExperienceLost()
        {
            XpOrbBuffer buffer = Create(2);
            buffer.Spawn(new Vector3(0f, 0f, 0f), Vector3.zero, 0f, 1);
            buffer.Spawn(new Vector3(10f, 0f, 0f), Vector3.zero, 0f, 1);

            int index = buffer.Spawn(new Vector3(9f, 0f, 0f), Vector3.zero, 0f, 5);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1, index);
            Assert.AreEqual(6, buffer.GetValue(1));
            Assert.AreEqual(7, buffer.TotalValue());
        }

        [Test]
        public void Full_PrefersALyingOrbOverACloserFlyingOne()
        {
            XpOrbBuffer buffer = Create(2);
            buffer.Spawn(new Vector3(0f, 0f, 0f), Vector3.zero, 0f, 1);
            buffer.Spawn(new Vector3(20f, 0f, 0f), Vector3.zero, 0f, 1);
            buffer.Step(DT, new Vector3(0f, 0f, 3f), true, 4f); // the first starts flying

            Assert.AreEqual(XpOrbBuffer.OrbState.Flying, buffer.GetState(0));
            int index = buffer.Spawn(new Vector3(1f, 0f, 0f), Vector3.zero, 0f, 4);

            Assert.AreEqual(1, index);
            Assert.AreEqual(5, buffer.GetValue(1));
        }

        [Test]
        public void TankInRadius_PullsAndCollects_OthersStay()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(3f, 0f, 0f), Vector3.zero, 0f, 2);
            buffer.Spawn(new Vector3(30f, 0f, 0f), Vector3.zero, 0f, 7);

            Run(buffer, 1f, Vector3.zero, true, 4f, out int collected);

            Assert.AreEqual(2, collected);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(7, buffer.GetValue(0));
            Assert.AreEqual(XpOrbBuffer.OrbState.Lying, buffer.GetState(0));
        }

        [Test]
        public void BiggerRadius_ReachesFurther()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(5f, 0f, 0f), Vector3.zero, 0f, 1);

            Run(buffer, 0.1f, Vector3.zero, true, 4f, out _);
            Assert.AreEqual(XpOrbBuffer.OrbState.Lying, buffer.GetState(0));

            Run(buffer, 1f, Vector3.zero, true, 4f * 1.5f, out int collected);
            Assert.AreEqual(1, collected);
        }

        [Test]
        public void PullAll_BringsEveryOrbIn()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(20f, 0f, 0f), Vector3.zero, 0f, 3);
            buffer.Spawn(new Vector3(-25f, 0f, 10f), Vector3.zero, 0f, 4);
            buffer.Spawn(new Vector3(0f, 2f, 30f), new Vector3(0f, 3f, 0f), 0f, 5);

            buffer.PullAll();
            Run(buffer, 3f, Vector3.zero, true, 4f, out int collected);

            Assert.AreEqual(12, collected);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Flying_Accelerates_UpToTheTopSpeed()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(200f, 0f, 0f), Vector3.zero, 0f, 1);
            buffer.PullAll();

            float x0 = buffer.GetPosition(0).x;
            buffer.Step(DT, Vector3.zero, true, 4f);
            float early = x0 - buffer.GetPosition(0).x;

            Run(buffer, 2f, Vector3.zero, true, 4f, out _);
            float before = buffer.GetPosition(0).x;
            buffer.Step(DT, Vector3.zero, true, 4f);
            float late = before - buffer.GetPosition(0).x;

            Assert.Greater(late, early);
            Assert.AreEqual(25f * DT, late, 1e-3f);
        }

        [Test]
        public void NoTank_FlyingOrbsWait()
        {
            XpOrbBuffer buffer = Create();
            buffer.Spawn(new Vector3(5f, 0f, 0f), Vector3.zero, 0f, 1);
            buffer.PullAll();

            Run(buffer, 1f, Vector3.zero, false, 4f, out int collected);

            Assert.AreEqual(0, collected);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), buffer.GetPosition(0));
        }

        [Test]
        public void ZeroValue_IsNotSpawned()
        {
            XpOrbBuffer buffer = Create();
            Assert.AreEqual(-1, buffer.Spawn(Vector3.zero, Vector3.zero, 0f, 0));
            Assert.AreEqual(0, buffer.Count);
        }

        [TestCase(EnemyKind.Crawler, 1, 1)]
        [TestCase(EnemyKind.Spitter, 3, 1)]
        [TestCase(EnemyKind.Exploder, 3, 1)]
        [TestCase(EnemyKind.EliteRegax, 50, 10)]
        [TestCase(EnemyKind.Boss, 300, 20)]
        public void Settings_XpPerKind(EnemyKind kind, int xp, int orbs)
        {
            var settings = new XpSettings();
            Assert.AreEqual(xp, settings.XpFor(kind));
            Assert.AreEqual(orbs, settings.OrbsFor(kind));
        }
    }
}
