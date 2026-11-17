using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class StunBuffTests
    {
        private StunBuffConfig _config;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StunBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_config, "Stun");
            _gameObject = new GameObject("Enemy");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_gameObject);
        }

        private IEntity Enemy(bool withAnimator = true)
        {
            IEntity enemy = new Entity();
            enemy.AddTransform(_gameObject.transform);
            if (withAnimator)
                enemy.AddAnimator(_gameObject.AddComponent<Animator>());
            enemy.AddBuffsEffects(new ReactiveList<BuffBase>());
            enemy.AddBehaviour(new TransformMoveBehaviour());
            enemy.AddBehaviour(new TransformRotateBehaviour());
            enemy.AddBehaviour(new EnemyCharacterChaseBehaviour());
            enemy.AddBehaviour(new EnemyCharacterAttackBehaviour());
            return enemy;
        }

        [Test]
        public void Apply_FreezesMovementAndAi()
        {
            IEntity enemy = Enemy();

            bool applied = BuffUseCase.Apply(enemy, _config.CreateBuff());

            Assert.That(applied, Is.True);
            Assert.That(enemy.HasBehaviour<TransformMoveBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<TransformRotateBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<EnemyCharacterChaseBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<EnemyCharacterAttackBehaviour>(), Is.False);
            Assert.That(_gameObject.transform.localScale, Is.EqualTo(Vector3.one * 1.5f));
        }

        [Test]
        public void Discard_GivesBackTheVerySameBehavioursAndScale()
        {
            IEntity enemy = Enemy();
            TransformMoveBehaviour move = enemy.GetBehaviour<TransformMoveBehaviour>();
            EnemyCharacterChaseBehaviour chase = enemy.GetBehaviour<EnemyCharacterChaseBehaviour>();
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            BuffUseCase.Discard(enemy, buff);

            Assert.That(enemy.GetBehaviour<TransformMoveBehaviour>(), Is.SameAs(move));
            Assert.That(enemy.GetBehaviour<EnemyCharacterChaseBehaviour>(), Is.SameAs(chase));
            Assert.That(enemy.HasBehaviour<TransformRotateBehaviour>(), Is.True);
            Assert.That(enemy.HasBehaviour<EnemyCharacterAttackBehaviour>(), Is.True);
            Assert.That(_gameObject.transform.localScale.x, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Discard_DoesNotAddBehavioursTheEnemyNeverHad()
        {
            IEntity enemy = Enemy();
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(enemy, buff);

            BuffUseCase.Discard(enemy, buff);

            Assert.That(enemy.HasBehaviour<EnemyCharacterPatrolBehaviour>(), Is.False);
            Assert.That(enemy.HasBehaviour<CharacterMoveAnimBehaviour>(), Is.False);
        }

        /// <summary>
        /// IsValid always says yes, but OnApply needs an Animator: on an entity without one it throws
        /// halfway, after scaling the body up and stripping its behaviours, with the buff behaviour
        /// already attached. A buff that cannot apply is supposed to refuse and change nothing.
        /// </summary>
        [Test]
        public void Apply_EntityWithoutAnimator_RefusesAndChangesNothing()
        {
            IEntity enemy = Enemy(withAnimator: false);
            BuffBase buff = _config.CreateBuff();
            bool applied = true;

            Assert.DoesNotThrow(() => applied = BuffUseCase.Apply(enemy, buff));
            Assert.That(applied, Is.False);
            Assert.That(_gameObject.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(enemy.HasBehaviour<TransformMoveBehaviour>(), Is.True);
        }
    }
}
