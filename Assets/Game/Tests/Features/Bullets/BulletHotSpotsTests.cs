using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The hot point a machine-gun bullet leaves where it struck: it cools from white-orange to dull red and out,
    /// and constant fire reuses a fixed ring of spots and lights without allocating.
    /// </summary>
    public sealed class BulletHotSpotsTests
    {
        [TearDown]
        public void TearDown()
        {
            GameObject go = GameObject.Find("<BulletHotSpots>");
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void Cooling_GoesFromBrightWhiteOrangeToDarkRed()
        {
            Color hot = BulletHotSpots.Cooling(0f);
            Color mid = BulletHotSpots.Cooling(0.5f);
            Color cold = BulletHotSpots.Cooling(1f);

            Assert.That(hot.a, Is.EqualTo(1f));
            Assert.That(cold.a, Is.EqualTo(0f));
            Assert.That(mid.a, Is.LessThan(hot.a).And.GreaterThan(cold.a));
            // Green (yellow-white) drains first: the glow reddens as it cools.
            Assert.That(hot.g, Is.GreaterThan(mid.g));
            Assert.That(mid.g, Is.GreaterThan(cold.g));
            Assert.That(cold.r, Is.GreaterThan(cold.g * 5f));
        }

        [Test]
        public void Emit_UnderConstantFire_CapsLightsAndSpots()
        {
            for (int i = 0; i < BulletHotSpots.MAX + 40; i++)
                BulletHotSpots.Emit(new Vector3(i * 0.1f, 0f, 0f), Vector3.up);

            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(BulletHotSpots.LIGHTS));
            Assert.That(BulletHotSpots.ActiveSpots, Is.EqualTo(BulletHotSpots.MAX));
        }

        [Test]
        public void Emit_AfterTheFirstHit_DoesNotAllocate()
        {
            BulletHotSpots.Emit(Vector3.zero, Vector3.up);

            Assert.That(() =>
            {
                for (int i = 0; i < 64; i++)
                    BulletHotSpots.Emit(new Vector3(i, 0f, 0f), Vector3.forward);
            }, UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not));
        }
    }
}
