using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankGunSwayBehaviourTests
    {
        private const float DT = 1f / 60f;
        private static readonly float Limit = 7f * Mathf.Deg2Rad;
        private static readonly float W = 2f * Mathf.PI * 1.4f;
        private static readonly float Stiffness = W * W;
        private static readonly float Damping = 2f * 0.22f * W;
        private static readonly float Heave = 0.004f * W * W;
        private static readonly float Surge = 0.012f * W * W;

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
        public void HardJolt_StopsAtTheLimit()
        {
            float angle = 0f, rate = 0f;
            Run(ref angle, ref rate, 0f, 200f, 0f, 0.5f);
            Assert.AreEqual(-Limit, angle, 1e-5f);
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
