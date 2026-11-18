using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankGunSwayBehaviourTests
    {
        private const float DT = 1f / 60f;
        private static readonly float Limit = 12f * Mathf.Deg2Rad;
        private static readonly float W = 2f * Mathf.PI * 1.4f;
        private static readonly float Stiffness = W * W;
        private static readonly float Damping = 2f * 0.1f * W;
        private static readonly float Heave = 0.008f * W * W;
        private static readonly float Surge = 0.025f * W * W;

        private static void Run(ref float angle, ref float rate, float hullRate, float lift, float push, float seconds)
        {
            for (float t = 0f; t < seconds; t += DT)
                TankGunSwayBehaviour.Step(ref angle, ref rate, hullRate, lift, push, Stiffness, Damping, Heave, Surge, Limit, DT);
        }

        [Test]
        public void HullStartingToPitchUp_GunLagsDownThenSettles()
        {
            float angle = 0f, rate = 0f;
            Run(ref angle, ref rate, 0.5f, 0f, 0f, 0.1f);
            Assert.Less(angle, 0f, "the gun keeps its pitch while the hull noses up under it");

            Run(ref angle, ref rate, 0.5f, 0f, 0f, 6f);
            Assert.AreEqual(0f, angle, 1e-3f, "at a steady pitch rate the spring brings it back to rest");
        }

        [Test]
        public void HardJolt_NeverPassesTheLimit()
        {
            float angle = 0f, rate = 0f, lowest = 0f;
            for (int i = 0; i < 60; i++)
            {
                Run(ref angle, ref rate, 0f, 200f, 0f, DT);
                Assert.LessOrEqual(Mathf.Abs(angle), Limit + 1e-6f);
                lowest = Mathf.Min(lowest, angle);
            }
            Assert.AreEqual(-Limit, lowest, 1e-5f, "a jolt that hard reaches the stop");
        }

        [Test]
        public void Nudge_SwingsAFewTimesDyingOut()
        {
            float angle = 0f, rate = 0.5f, previous = 0f;
            int crossings = 0;
            float firstPeak = 0f, lastPeak = 0f, peak = 0f;
            for (float t = 0f; t < 3f; t += DT)
            {
                Run(ref angle, ref rate, 0f, 0f, 0f, DT);
                peak = Mathf.Max(peak, Mathf.Abs(angle));
                if (previous != 0f && Mathf.Sign(angle) != Mathf.Sign(previous))
                {
                    crossings++;
                    if (firstPeak == 0f) firstPeak = peak;
                    lastPeak = peak;
                    peak = 0f;
                }
                previous = angle;
            }
            Assert.GreaterOrEqual(crossings, 4, "it swings back and forth a few times");
            Assert.Less(lastPeak, firstPeak * 0.5f, "each swing smaller than the one before");
        }

        [Test]
        public void HullAtRest_GunStaysAtRest()
        {
            float angle = 0f, rate = 0f;
            Run(ref angle, ref rate, 0f, 0f, 0f, 1f);
            Assert.AreEqual(0f, angle);
            Assert.AreEqual(0f, rate);
        }
    }
}
