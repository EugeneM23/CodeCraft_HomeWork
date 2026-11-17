using System.Collections.Generic;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DamageCastBehaviourTests
    {
        private const string CAST_EVENT = "damage_cast_event";

        private readonly List<GameObject> _created = new();
        private AnimationEventReceiver _receiver;
        private IEntity _character;
        private IEntity _blade;
        private DamageCastBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _receiver = Track(new GameObject("Rig")).AddComponent<AnimationEventReceiver>();
            _blade = Blade();
            _character = new Entity();
            _character.AddAnimationEventReceiver(_receiver);
            _character.AddWeapon(new ReactiveVariable<IEntity>(_blade));
            _behaviour = new DamageCastBehaviour();
            _behaviour.Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            _behaviour.Dispose(_character);
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private IEntity Blade()
        {
            IEntity blade = new Entity();
            blade.AddTransform(Track(new GameObject("Blade")).transform);
            blade.AddDamageRadius(new Const<float>(1f));
            blade.AddDamageLayer(~0);
            blade.AddDamage(new ReactiveInt(3));
            return blade;
        }

        private Health Victim(Vector3 position)
        {
            GameObject gameObject = Track(new GameObject("Victim"));
            gameObject.transform.position = position;
            gameObject.AddComponent<SphereCollider>().radius = 0.3f;
            var entity = gameObject.AddComponent<MonoEntity>();
            ((ISerializationCallbackReceiver)entity).OnAfterDeserialize();
            entity.AddDamageableTag();
            var health = new Health(10, 10);
            entity.AddHealth(health);
            entity.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            Physics.SyncTransforms();
            return health;
        }

        /// <summary>What an animation event on the clip does: Unity calls ReceiveEvent by name.</summary>
        private void AnimationEvent(string name) =>
            typeof(AnimationEventReceiver)
                .GetMethod("ReceiveEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_receiver, new object[] { name });

        [Test]
        public void CastEvent_VictimInReach_TakesTheBladesDamage()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));

            AnimationEvent(CAST_EVENT);
            _behaviour.Tick(_character, 0.016f);

            Assert.That(health.GetCurrent(), Is.EqualTo(7));
        }

        [Test]
        public void NoCastEvent_VictimInReach_IsNotHit()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));

            _behaviour.Tick(_character, 0.016f);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }

        [Test]
        public void OtherAnimationEvent_DoesNotCast()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));

            AnimationEvent("footstep");
            _behaviour.Tick(_character, 0.016f);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }

        [Test]
        public void OneCast_HitsOnlyOnce()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));

            AnimationEvent(CAST_EVENT);
            _behaviour.Tick(_character, 0.01f);
            _behaviour.Tick(_character, 0.01f);
            _behaviour.Tick(_character, 0.01f);

            Assert.That(health.GetCurrent(), Is.EqualTo(7));
        }

        [Test]
        public void VictimSteppingInWithinTheCastWindow_IsHit()
        {
            AnimationEvent(CAST_EVENT);
            _behaviour.Tick(_character, 0.03f);
            Health health = Victim(new Vector3(0.5f, 0f, 0f));
            _behaviour.Tick(_character, 0.03f);

            Assert.That(health.GetCurrent(), Is.EqualTo(7));
        }

        [Test]
        public void VictimSteppingInAfterTheCastWindow_IsNotHit()
        {
            AnimationEvent(CAST_EVENT);
            _behaviour.Tick(_character, 0.2f);
            Health health = Victim(new Vector3(0.5f, 0f, 0f));
            _behaviour.Tick(_character, 0.016f);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }

        [Test]
        public void SwappedToAFirearmMidCast_DoesNotThrow_AndEndsTheCast()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));
            IEntity rifle = new Entity();

            AnimationEvent(CAST_EVENT);
            _character.GetWeapon().Value = rifle;
            Assert.DoesNotThrow(() => _behaviour.Tick(_character, 0.01f));

            _character.GetWeapon().Value = _blade;
            _behaviour.Tick(_character, 0.01f);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }

        [Test]
        public void AfterDispose_CastEventIsIgnored()
        {
            Health health = Victim(new Vector3(0.5f, 0f, 0f));
            var behaviour = new DamageCastBehaviour();
            behaviour.Init(_character);
            behaviour.Dispose(_character);
            _behaviour.Dispose(_character);

            AnimationEvent(CAST_EVENT);
            behaviour.Tick(_character, 0.016f);
            _behaviour.Tick(_character, 0.016f);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }
    }
}
