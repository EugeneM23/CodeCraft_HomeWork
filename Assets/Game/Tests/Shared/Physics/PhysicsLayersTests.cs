using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PhysicsLayersTests
    {
        private static bool In(int mask, int layer) => (mask & (1 << layer)) != 0;

        [Test]
        public void Targets_TakeEverythingThatCanBeHurt()
        {
            Assert.That(In(PhysicsLayers.Targets, PhysicsLayers.Character), Is.True);
            Assert.That(In(PhysicsLayers.Targets, PhysicsLayers.Enemy), Is.True);
            Assert.That(In(PhysicsLayers.Targets, PhysicsLayers.Dummy), Is.True);
            Assert.That(In(PhysicsLayers.Targets, 0), Is.True, "Default");
        }

        [Test]
        public void Targets_LeaveOutTheGroundBulletsAndEveryDestructiblePiece()
        {
            Assert.That(In(PhysicsLayers.Targets, PhysicsLayers.Ground), Is.False);
            Assert.That(In(PhysicsLayers.Targets, PhysicsLayers.Bullet), Is.False);
            Assert.That(PhysicsLayers.Targets & PhysicsLayers.DestructiblePieces, Is.Zero);
        }

        [Test]
        public void DestructiblePieces_CoverStandingAndKnockedOutPieces()
        {
            Assert.That(In(PhysicsLayers.DestructiblePieces, PhysicsLayers.Destructible), Is.True);
            Assert.That(In(PhysicsLayers.DestructiblePieces, PhysicsLayers.Debris), Is.True);
            Assert.That(In(PhysicsLayers.DestructiblePieces, PhysicsLayers.Rubble), Is.True);
            Assert.That(In(PhysicsLayers.DestructiblePieces, PhysicsLayers.Character), Is.False);
        }
    }
}
