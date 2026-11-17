using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EffectAreaBehaviourTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private MoveSpeedBuffConfig _slow;
        private TriggerEventReceiver _trigger;
        private MonoEntity _character;
        private EffectAreaBehaviour _area;

        [SetUp]
        public void SetUp()
        {
            _slow = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", Private).SetValue(_slow, "ZoneSlow");
            typeof(MoveSpeedBuffConfig).GetField("<Speed>k__BackingField", Private).SetValue(_slow, 0.5f);

            _trigger = new GameObject("Area").AddComponent<TriggerEventReceiver>();
            _area = new EffectAreaBehaviour(_trigger, new BuffConfig[] { _slow, null });
            _area.Init(new Entity());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_slow);
            Object.DestroyImmediate(_trigger.gameObject);
            if (_character != null)
                Object.DestroyImmediate(_character.gameObject);
        }

        private Collider Character(bool damageable = true)
        {
            _character = MonoEntity.Create<MonoEntity>(
                name: "Character", installOnAwake: false, useUnityLifecycle: false);
            if (damageable)
                _character.AddDamageableTag();
            _character.AddMoveSpeed(new ReactiveFloat(4f));
            _character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return _character.gameObject.AddComponent<BoxCollider>();
        }

        private void Enter(Collider collider) =>
            typeof(TriggerEventReceiver).GetMethod("OnTriggerEnter", Private).Invoke(_trigger, new object[] { collider });

        private void Exit(Collider collider) =>
            typeof(TriggerEventReceiver).GetMethod("OnTriggerExit", Private).Invoke(_trigger, new object[] { collider });

        private float Speed => _character.GetMoveSpeed().Value;

        [Test]
        public void Enter_GivesTheAreaBuffs()
        {
            Collider collider = Character();

            Enter(collider);

            Assert.That(_character.GetBuffsEffects(), Has.Count.EqualTo(1));
            Assert.That(Speed, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Exit_TakesThemBack()
        {
            Collider collider = Character();
            Enter(collider);

            Exit(collider);

            Assert.That(_character.GetBuffsEffects(), Is.Empty);
            Assert.That(Speed, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void SecondColliderOfTheSameEntity_BuffsOnlyOnce_AndKeepsThemUntilTheLastLeaves()
        {
            Collider first = Character();
            Collider second = _character.gameObject.AddComponent<SphereCollider>();

            Enter(first);
            Enter(second);
            float bothInside = Speed;
            Exit(first);
            float oneInside = Speed;
            Exit(second);

            Assert.That(bothInside, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(oneInside, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Speed, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void NotDamageable_IsIgnored()
        {
            Collider collider = Character(damageable: false);

            Enter(collider);

            Assert.That(_character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void ColliderWithoutEntity_IsIgnored()
        {
            var stray = new GameObject("Bullet");
            try
            {
                Collider collider = stray.AddComponent<BoxCollider>();

                Assert.DoesNotThrow(() => Enter(collider));
                Assert.DoesNotThrow(() => Exit(collider));
            }
            finally
            {
                Object.DestroyImmediate(stray);
            }
        }

        [Test]
        public void Exit_LeavesTheSameBuffFromAnotherSourceAlone()
        {
            Collider collider = Character();
            BuffBase pickup = _slow.CreateBuff();
            BuffUseCase.Apply(_character, pickup);

            Enter(collider);
            Exit(collider);

            Assert.That(_character.GetBuffsEffects(), Does.Contain(pickup));
            Assert.That(Speed, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Dispose_TakesBackFromWhoeverIsStillInside()
        {
            Collider collider = Character();
            Enter(collider);

            _area.Dispose(new Entity());

            Assert.That(_character.GetBuffsEffects(), Is.Empty);
            Assert.That(Speed, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void Dispose_StopsListeningToTheTrigger()
        {
            Collider collider = Character();

            _area.Dispose(new Entity());
            Enter(collider);

            Assert.That(_character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Dispose_WithADestroyedCharacterInside_DoesNotThrow()
        {
            Collider collider = Character();
            Enter(collider);

            Object.DestroyImmediate(_character.gameObject);

            Assert.DoesNotThrow(() => _area.Dispose(new Entity()));
        }
    }
}
