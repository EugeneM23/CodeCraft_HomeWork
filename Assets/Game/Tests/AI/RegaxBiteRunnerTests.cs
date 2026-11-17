using AIKit;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    public sealed class RegaxBiteRunnerTests
    {
        private GameObject _tank;
        private GameObject _regax;
        private PlayerTank _player;
        private Entity _entity;
        private RegaxBiteRunner _runner;
        private int _fired;
        private bool _weaponReady;

        [SetUp]
        public void SetUp()
        {
            _tank = new GameObject("Tank");
            var box = _tank.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(3.5f, 2f, 6.5f);
            _player = new PlayerTank();
            _player.Bind(_tank.transform);

            _regax = new GameObject("Regax");
            _fired = 0;
            _weaponReady = true;
            var weapon = new Entity("Hand");
            weapon.AddFireCondition(new AndExpression(new ExpressionMember<bool>(() => _weaponReady)));
            _entity = new Entity("Regax");
            _entity.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            _entity.AddFireAction(new InlineAction(() => _fired++));
            _entity.AddFireCondition(new AndExpression());
            _runner = new RegaxBiteRunner(_regax.transform, _player, _entity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tank);
            Object.DestroyImmediate(_regax);
        }

        private void StandBesideTheHull(float gap) => _regax.transform.position = new Vector3(1.75f + gap, 0f, 2f);

        [Test]
        public void Reach_IsMeasuredToTheHullsSide_NotItsMiddle()
        {
            Assert.IsTrue(_runner.InReach(new Vector3(1.75f + 1f, 0f, 2f)), "1 m off the side: 3 m from the middle");
            Assert.IsTrue(_runner.InReach(new Vector3(0f, 0f, 3.25f + 1.5f)), "1.5 m off the nose");
            Assert.IsFalse(_runner.InReach(new Vector3(1.75f + 2.5f, 0f, 0f)));
        }

        [Test]
        public void OutOfReach_CannotStart()
        {
            StandBesideTheHull(3f);

            Assert.IsFalse(_runner.CanStart(null));
        }

        [Test]
        public void InReach_StartsAndFiresTheHeldWeapon()
        {
            StandBesideTheHull(0.8f);

            Assert.IsTrue(_runner.CanStart(null));
            _runner.Begin(null);

            Assert.That(_fired, Is.EqualTo(1));
            Assert.IsTrue(_runner.IsRunning);
            Assert.That(_runner.Phase, Is.EqualTo(AbilityPhase.WindUp));
            Assert.IsFalse(_runner.CanStart(null), "one bite at a time");
        }

        [Test]
        public void WeaponNotReady_CannotStart()
        {
            StandBesideTheHull(0.8f);
            _weaponReady = false;

            Assert.IsFalse(_runner.CanStart(null));
        }

        [Test]
        public void Phases_WindUpActiveRecovery_ThenOver()
        {
            StandBesideTheHull(0.8f);
            _runner.Begin(null);

            _runner.Tick(RegaxBiteRunner.WIND_UP + 0.01f);
            Assert.That(_runner.Phase, Is.EqualTo(AbilityPhase.Active));
            _runner.Tick(RegaxBiteRunner.ACTIVE);
            Assert.That(_runner.Phase, Is.EqualTo(AbilityPhase.Recovery));
            _runner.Tick(RegaxBiteRunner.RECOVERY);
            Assert.IsFalse(_runner.IsRunning);
            Assert.That(_runner.Phase, Is.EqualTo(AbilityPhase.None));
        }

        [Test]
        public void Cancel_EndsIt()
        {
            StandBesideTheHull(0.8f);
            _runner.Begin(null);
            _runner.Cancel();

            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void DeadTank_OutOfReach()
        {
            var tankEntity = new Entity("Tank");
            tankEntity.AddTransform(_tank.transform);
            var health = new Health(10, 10);
            tankEntity.AddHealth(health);
            _player.Bind(tankEntity);
            StandBesideTheHull(0.8f);
            Assert.IsTrue(_runner.CanStart(null));

            health.Reduce(10);

            Assert.IsFalse(_runner.CanStart(null));
        }
    }
}
