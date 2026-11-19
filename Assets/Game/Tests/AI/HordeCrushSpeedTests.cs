using Atomic.Entities;
using NUnit.Framework;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// The horde crushes at the player's vehicle's own threshold (<see cref="TankCrushBehaviour.MinSpeed"/>, which the
    /// loadout sets), never at a number of its own; a vehicle that does not crush crushes no monster either.
    /// </summary>
    public sealed class HordeCrushSpeedTests
    {
        [Test]
        public void Tank_GivesItsCrushThreshold()
        {
            Entity tank = TankWithCrush(1.5f, out _);

            Assert.AreEqual(1.5f, HordeBehaviour.CrushSpeedOf(tank));
        }

        [Test]
        public void Threshold_FollowsTheLoadout()
        {
            Entity tank = TankWithCrush(1.5f, out TankCrushBehaviour crush);

            crush.MinSpeed = 0.75f; // tracks module (LoadoutApplyUseCase)

            Assert.AreEqual(0.75f, HordeBehaviour.CrushSpeedOf(tank));
        }

        [Test]
        public void NoVehicle_OrOneThatDoesNotCrush_CrushesNothing()
        {
            Assert.AreEqual(HordeBehaviour.NO_CRUSH, HordeBehaviour.CrushSpeedOf(null));
            Assert.AreEqual(HordeBehaviour.NO_CRUSH, HordeBehaviour.CrushSpeedOf(new Entity()));
            Assert.Greater(HordeBehaviour.NO_CRUSH, 1e6f, "no speed is fast enough");
        }

        [Test]
        public void ReadingTheThreshold_AllocatesNothing()
        {
            Entity tank = TankWithCrush(1.5f, out _);
            float read = 0f;

            Assert.That(() => { read = HordeBehaviour.CrushSpeedOf(tank); }, NoAllocation.Constraint);
            Assert.AreEqual(1.5f, read);
        }

        /// <summary>A bare entity with the tank's crush behaviour (not initialised: only its threshold is read).</summary>
        private static Entity TankWithCrush(float minSpeed, out TankCrushBehaviour crush)
        {
            var tank = new Entity();
            crush = new TankCrushBehaviour(null, null, new TankCrushConfig { minSpeed = minSpeed });
            tank.AddBehaviour(crush);
            return tank;
        }
    }
}
