using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Where the blood of a crushed enemy sticks to the tank (TankCrushBehaviour.TryGetStainChance),
    /// without the dice: the splashes themselves are random, the chance of each spot is not. The
    /// tank faces +Z, its nose at z = 2.2, its tracks at x = ±0.9, the ground at y = 0.
    /// </summary>
    public sealed class TankCrushBehaviourTests
    {
        private const float REACH = 2.5f;
        private const float THICK_BELOW = 1.2f;
        private const float GROUND = 0f;

        private static readonly Vector3 Front = Vector3.forward;

        private static float Chance(Vector3 at, Vector3 normal, Vector3 burst)
        {
            Assert.IsTrue(TankCrushBehaviour.TryGetStainChance(at, normal, burst, GROUND, REACH, THICK_BELOW,
                out float chance), "within reach");
            return chance;
        }

        [Test]
        public void BurstOffToTheRight_TheRightFrontTakesClearlyMoreThanTheLeft()
        {
            var burst = new Vector3(0.8f, 0.3f, 2.6f);

            float right = Chance(new Vector3(0.9f, 0.5f, 2.2f), Front, burst);
            float left = Chance(new Vector3(-0.9f, 0.5f, 2.2f), Front, burst);

            Assert.That(left, Is.GreaterThan(0f), "the left front is in reach too");
            Assert.That(right, Is.GreaterThan(left * 1.3f));
        }

        [Test]
        public void BurstHeadOn_BothFrontsAlike()
        {
            var burst = new Vector3(0f, 0.3f, 2.6f);

            float right = Chance(new Vector3(0.9f, 0.5f, 2.2f), Front, burst);
            float left = Chance(new Vector3(-0.9f, 0.5f, 2.2f), Front, burst);

            Assert.That(right, Is.EqualTo(left).Within(1e-5f));
        }

        [Test]
        public void TheBack_IsOutOfReach()
        {
            var burst = new Vector3(0f, 0.3f, 2.6f);

            Assert.IsFalse(TankCrushBehaviour.TryGetStainChance(new Vector3(0f, 0.5f, -2.2f), Vector3.back, burst,
                GROUND, REACH, THICK_BELOW, out _));
        }

        [Test]
        public void AFaceTurnedAway_TakesNone()
        {
            var burst = new Vector3(0f, 0.3f, 2.6f);

            Assert.That(Chance(new Vector3(0f, 0.5f, 1.6f), Vector3.back, burst), Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void HigherUp_TakesLess()
        {
            var burst = new Vector3(0f, 0.3f, 3.4f);

            float low = Chance(new Vector3(0f, 1.0f, 2.2f), Front, burst);
            float high = Chance(new Vector3(0f, 1.8f, 2.2f), Front, burst);

            Assert.That(high, Is.LessThan(low));
        }

        [Test]
        public void APartOneVoxelThin_TakesItFromEitherSide()
        {
            var burst = new Vector3(0f, 0.3f, 2.6f);

            Assert.That(Chance(new Vector3(0f, 0.5f, 1.6f), Vector3.zero, burst), Is.GreaterThan(0f));
        }
    }
}
