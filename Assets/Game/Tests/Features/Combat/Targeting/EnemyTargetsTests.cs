using Atomic.Elements;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EnemyTargetsTests
    {
        [Test]
        public void Nearest_IsTheBestAcrossSources()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            var b = new FakeEnemyTargetSource();
            a.Add(new Vector3(10f, 0f, 0f));
            b.Add(new Vector3(0f, 0f, 4f));
            b.Add(new Vector3(0f, 0f, 12f));
            targets.Register(a);
            int idB = targets.Register(b);

            bool found = targets.TryFindNearest(Vector3.zero, 18f, out EnemyRef target, out Vector3 position);

            Assert.That(found, Is.True);
            Assert.That(target.Source, Is.EqualTo(idB));
            Assert.That(target.Index, Is.EqualTo(0));
            Assert.That(position, Is.EqualTo(new Vector3(0f, 0f, 4f)));
        }

        [Test]
        public void Nearest_OutOfRange_FindsNothing()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            a.Add(new Vector3(30f, 0f, 0f));
            targets.Register(a);

            Assert.That(targets.TryFindNearest(Vector3.zero, 18f, out _, out _), Is.False);
        }

        [Test]
        public void NearestBeyond_SkipsEnemiesInsideTheBlast()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            a.Add(new Vector3(2f, 0f, 0f));
            a.Add(new Vector3(9f, 0f, 0f));
            targets.Register(a);

            bool found = targets.TryFindNearestBeyond(Vector3.zero, 5f, 15f, out EnemyRef target, out _);

            Assert.That(found, Is.True);
            Assert.That(target.Index, Is.EqualTo(1));
        }

        [Test]
        public void Densest_PicksTheCrowdOverALoneEnemy()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            var b = new FakeEnemyTargetSource();
            a.Add(new Vector3(-5f, 0f, 0f));
            Vector3 crowd = new Vector3(15f, 0f, 10f);
            for (int i = 0; i < 4; i++)
                b.Add(crowd + new Vector3(i * 0.5f, 0f, (i % 2) * 0.5f));
            a.Add(crowd + new Vector3(-0.5f, 0f, 0f));
            targets.Register(a);
            targets.Register(b);

            bool found = targets.TryFindDensest(Vector3.zero, 30f, 4f, out Vector3 point);

            Assert.That(found, Is.True);
            Assert.That(targets.CountInRadius(point, 4f), Is.EqualTo(5));
            Assert.That(Vector3.Distance(point, crowd), Is.LessThan(4f));
        }

        [Test]
        public void Densest_WithoutEnemies_FindsNothing()
        {
            var targets = new EnemyTargets();
            targets.Register(new FakeEnemyTargetSource());

            Assert.That(targets.TryFindDensest(Vector3.zero, 30f, 4f, out _), Is.False);
        }

        [Test]
        public void Ref_OfAKilledOrReusedSlot_IsNoLongerAlive()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            a.Add(new Vector3(3f, 0f, 0f));
            targets.Register(a);
            targets.TryFindNearest(Vector3.zero, 18f, out EnemyRef target, out _);
            Assert.That(targets.IsAlive(target), Is.True);

            a.Kill(0);
            a.Respawn(0, new Vector3(4f, 0f, 0f));

            Assert.That(targets.IsAlive(target), Is.False);
            targets.Damage(target, 10f, Vector3.zero);
            Assert.That(a.Dealt[0], Is.EqualTo(0f), "a stale ref hurts nobody");
        }

        [Test]
        public void Unregistered_Source_IsNoLongerAsked()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            a.Add(new Vector3(3f, 0f, 0f));
            int id = targets.Register(a);
            targets.TryFindNearest(Vector3.zero, 18f, out EnemyRef target, out _);

            targets.Unregister(id);

            Assert.That(targets.AliveCount, Is.EqualTo(0));
            Assert.That(targets.IsAlive(target), Is.False);
            Assert.That(targets.TryFindNearest(Vector3.zero, 18f, out _, out _), Is.False);
        }

        [Test]
        public void Raycast_HitsTheFirstAlongTheSegment_AcrossSources()
        {
            var targets = new EnemyTargets();
            var a = new FakeEnemyTargetSource();
            var b = new FakeEnemyTargetSource();
            a.Add(new Vector3(0f, 0f, 8f));
            b.Add(new Vector3(0f, 0f, 3f));
            targets.Register(a);
            int idB = targets.Register(b);

            bool hit = targets.Raycast(Vector3.zero, new Vector3(0f, 0f, 10f), 0.3f, out EnemyRef target,
                out Vector3 point);

            Assert.That(hit, Is.True);
            Assert.That(target.Source, Is.EqualTo(idB));
            Assert.That(point.z, Is.EqualTo(3f - 0.8f).Within(0.001f));
        }

        [Test]
        public void NonPhysicalQueries_LeaveTheEntitySourceOut()
        {
            var targets = new EnemyTargets();
            var entities = new FakePhysicalEnemyTargetSource();
            var horde = new FakeEnemyTargetSource();
            entities.Add(new Vector3(0f, 0f, 3f));
            horde.Add(new Vector3(0f, 0f, 6f));
            targets.Register(entities);
            int hordeId = targets.Register(horde);

            bool hit = targets.RaycastNonPhysical(Vector3.zero, new Vector3(0f, 0f, 10f), 0.3f, out EnemyRef target,
                out _);
            targets.DamageAreaNonPhysical(Vector3.zero, 10f, 100f, 0.25f);

            Assert.That(hit, Is.True);
            Assert.That(target.Source, Is.EqualTo(hordeId));
            Assert.That(entities.AreaCalls, Is.EqualTo(0));
            Assert.That(horde.AreaCalls, Is.EqualTo(1));

            targets.DamageArea(Vector3.zero, 10f, 100f, 0.25f);
            Assert.That(entities.AreaCalls, Is.EqualTo(1), "the air strike's area reaches entity enemies too");
        }

        [Test]
        public void Pierce_IsUsedUpOneEnemyAtATime()
        {
            var pierce = new Variable<int>(2);

            Assert.That(BulletShotUseCase.TryUse(pierce), Is.True);
            Assert.That(BulletShotUseCase.TryUse(pierce), Is.True);
            Assert.That(BulletShotUseCase.TryUse(pierce), Is.False, "out of pierce: the shell bursts");
            Assert.That(pierce.Value, Is.EqualTo(0));
        }

        [Test]
        public void Ricochet_BouncesWhileTheSkillAllowsMore()
        {
            Assert.That(ShellBounceUseCase.ShouldBounce(0, 0), Is.False);
            Assert.That(ShellBounceUseCase.ShouldBounce(2, 1), Is.True);
            Assert.That(ShellBounceUseCase.ShouldBounce(2, 2), Is.False);
        }

        [Test]
        public void Aim_ClampsThePitch()
        {
            bool aimed = TargetGeometry.TryAim(Vector3.zero, new Vector3(0f, -10f, 1f), 20f, out Quaternion rotation);

            Assert.That(aimed, Is.True);
            float pitch = Mathf.DeltaAngle(0f, rotation.eulerAngles.x);
            Assert.That(pitch, Is.EqualTo(20f).Within(0.01f), "looking down is +x, clamped to 20 degrees");
        }
    }
}
