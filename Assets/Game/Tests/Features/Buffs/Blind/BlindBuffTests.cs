using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BlindBuffTests
    {
        private BlindBuffConfig _config;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<BlindBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_config, "Blind");
            _player = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_player);
        }

        private IEntity Player()
        {
            IEntity player = new Entity();
            player.AddTransform(_player.transform);
            return player;
        }

        private static IEntity Enemy(IEntity target)
        {
            IEntity enemy = new Entity();
            enemy.AddTarget(new ReactiveVariable<IEntity>(target));
            enemy.AddMoveDirection(new ReactiveVector3(Vector3.forward));
            enemy.AddRotateDirection(new ReactiveVector3(Vector3.forward));
            enemy.AddBuffsEffects(new ReactiveList<BuffBase>());
            enemy.AddBehaviour(new EnemyCharacterChaseBehaviour());
            enemy.AddBehaviour(new EnemyCharacterAttackBehaviour());
            return enemy;
        }

        [Test]
        public void Apply_LosesTargetAndStumblesInsteadOfChasing()
        {
            IEntity enemy = Enemy(Player());

            bool applied = BuffUseCase.Apply(enemy, _config.CreateBuff());

            Assert.That(applied, Is.True);
            Assert.That(enemy.GetTarget().Value, Is.Null);
            Assert.That(enemy.HasBehaviour<EnemyCharacterChaseBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<EnemyCharacterAttackBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<BlindStumbleBehaviour>(), Is.True);
        }

        [Test]
        public void Apply_EntityWithoutTarget_Refused()
        {
            IEntity enemy = new Entity();
            enemy.AddMoveDirection(new ReactiveVector3());
            enemy.AddRotateDirection(new ReactiveVector3());
            enemy.AddBuffsEffects(new ReactiveList<BuffBase>());

            bool applied = BuffUseCase.Apply(enemy, _config.CreateBuff());

            Assert.That(applied, Is.False);
            Assert.That(enemy.HasBehaviour<BlindStumbleBehaviour>(), Is.False);
        }

        [Test]
        public void Discard_ResumesChasingTheSameTargetAndStopsStumbling()
        {
            IEntity player = Player();
            IEntity enemy = Enemy(player);
            EnemyCharacterChaseBehaviour chase = enemy.GetBehaviour<EnemyCharacterChaseBehaviour>();
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            BuffUseCase.Discard(enemy, buff);

            Assert.That(enemy.GetTarget().Value, Is.SameAs(player));
            Assert.That(enemy.GetBehaviour<EnemyCharacterChaseBehaviour>(), Is.SameAs(chase));
            Assert.That(enemy.HasBehaviour<EnemyCharacterAttackBehaviour>(), Is.True);
            Assert.That(enemy.HasBehaviour<BlindStumbleBehaviour>(), Is.False);
            Assert.That(enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Discard_TargetDiedMeanwhile_StaysWithoutTarget()
        {
            IEntity player = Player();
            IEntity enemy = Enemy(player);
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            player.Dispose();
            BuffUseCase.Discard(enemy, buff);

            Assert.That(enemy.GetTarget().Value, Is.Null);
        }

        [Test]
        public void Discard_NewTargetGivenMeanwhile_KeepsTheNewOne()
        {
            IEntity enemy = Enemy(Player());
            IEntity other = Player();
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            enemy.GetTarget().Value = other;
            BuffUseCase.Discard(enemy, buff);

            Assert.That(enemy.GetTarget().Value, Is.SameAs(other));
        }

        [Test]
        public void Discard_AfterTheEnemyDied_DoesNotThrow()
        {
            IEntity enemy = Enemy(Player());
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            enemy.Dispose();

            Assert.DoesNotThrow(() => buff.Discard(enemy));
        }
    }
}
