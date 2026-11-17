using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BulletHitUseCaseTests
    {
        private static IEntity Projectile(int ricochets, bool explosive)
        {
            IEntity projectile = new Entity();
            projectile.AddRicochetMaxCount(ricochets);
            if (explosive)
                projectile.AddExplosionRadius(5f);
            return projectile;
        }

        private static IEntity Damageable(bool vehicle)
        {
            IEntity target = new Entity();
            target.AddDamageableTag();
            if (vehicle)
                target.AddVehicleTag();
            return target;
        }

        [Test]
        public void TankShell_OffAWall_TriesToRicochet()
        {
            Assert.That(BulletHitUseCase.ShouldTryRicochet(Projectile(2, explosive: true), null), Is.True);
        }

        [Test]
        public void TankShell_IntoSomethingThatTakesDamage_DoesNotRicochet()
        {
            IEntity shell = Projectile(2, explosive: true);

            Assert.That(BulletHitUseCase.ShouldTryRicochet(shell, Damageable(vehicle: false)), Is.False, "a person");
            Assert.That(BulletHitUseCase.ShouldTryRicochet(shell, Damageable(vehicle: true)), Is.False, "a tank");
        }

        [Test]
        public void Rocket_WithoutRicochet_NeverTries()
        {
            Assert.That(BulletHitUseCase.ShouldTryRicochet(Projectile(0, explosive: true), null), Is.False);
        }

        [Test]
        public void Bullet_OffAWall_StillTriesToRicochet()
        {
            Assert.That(BulletHitUseCase.ShouldTryRicochet(Projectile(2, explosive: false), new Entity()), Is.True);
        }

        [Test]
        public void HitOnArmour_ShowsAnEffect_HitOnFleshLeavesItToTheBlood()
        {
            Assert.That(BulletHitUseCase.ShowsHitOnTarget(Damageable(vehicle: true)), Is.True);
            Assert.That(BulletHitUseCase.ShowsHitOnTarget(Damageable(vehicle: false)), Is.False);
        }
    }
}
