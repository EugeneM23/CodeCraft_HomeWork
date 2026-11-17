using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WalkingUseCaseTests
    {
        private bool _weaponLetsWalk;

        [SetUp]
        public void SetUp() => _weaponLetsWalk = true;

        private IEntity Walker(out Health health, Vector3 facing = default)
        {
            health = new Health(10, 10);
            IEntity weapon = new Entity();
            weapon.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => _weaponLetsWalk)));

            IEntity walker = new Entity();
            walker.AddHealth(health);
            WalkingUseCase.AddWalking(walker, 4f, 10f, facing);
            walker.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            return walker;
        }

        [Test]
        public void AliveAndFreeHanded_MayWalk()
        {
            IEntity walker = Walker(out _);

            Assert.That(walker.GetMoveCondition().Invoke(), Is.True);
        }

        [Test]
        public void MeleeSwingRootingTheWeapon_StopsTheWalk()
        {
            IEntity walker = Walker(out _);

            _weaponLetsWalk = false;

            Assert.That(walker.GetMoveCondition().Invoke(), Is.False);
        }

        [Test]
        public void Dead_MayNotWalk()
        {
            IEntity walker = Walker(out Health health);

            health.Reduce(10);

            Assert.That(walker.GetMoveCondition().Invoke(), Is.False);
        }

        [Test]
        public void WeaponSwapped_ConditionAsksTheNewWeapon()
        {
            IEntity walker = Walker(out _);
            IEntity rooting = new Entity();
            rooting.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => false)));

            walker.GetWeapon().Value = rooting;

            Assert.That(walker.GetMoveCondition().Invoke(), Is.False);
        }

        [Test]
        public void StartsStill_FacingTheGivenWay()
        {
            IEntity walker = Walker(out _, Vector3.left);

            Assert.That(walker.GetMoveDirection().Value, Is.EqualTo(Vector3.zero));
            Assert.That(walker.GetRotateDirection().Value, Is.EqualTo(Vector3.left));
            Assert.That(walker.GetMoveSpeed().Value, Is.EqualTo(4f));
            Assert.That(walker.GetRotationSpeed().Value, Is.EqualTo(10f));
        }
    }
}
