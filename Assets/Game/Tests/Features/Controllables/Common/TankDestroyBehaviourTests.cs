using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankDestroyBehaviourTests
    {
        // Far from anything a scene open in the editor might hold, so the exit checks only meet
        // what a test puts there itself.
        private static readonly Vector3 Origin = new(-4700f, 300f, 4700f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color Paint = new(1f, 0.5f, 0.25f, 0.5f);

        private readonly List<Object> _objects = new();

        private IEntity _tank;
        private IEntity _driver;
        private Rigidbody _body;
        private Renderer _hullRenderer;
        private Event<TakeDamageArgs> _death;
        private ReactiveVariable<IEntity> _character;
        private GameContext _gameContext;

        [SetUp]
        public void SetUp()
        {
            GameObject tankObject = Track(new GameObject("Tank"));
            tankObject.transform.position = Origin;
            _body = tankObject.AddComponent<Rigidbody>();
            _body.useGravity = false;

            GameObject hullVisual = Track(new GameObject("HullVisual"));
            hullVisual.transform.SetParent(tankObject.transform, false);
            GameObject armour = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Object.DestroyImmediate(armour.GetComponent<Collider>());
            armour.transform.SetParent(hullVisual.transform, false);
            _hullRenderer = armour.GetComponent<Renderer>();

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader != null)
            {
                Material material = Track(new Material(shader));
                material.SetColor(ColorId, Paint);
                _hullRenderer.sharedMaterial = material;
            }

            _death = new Event<TakeDamageArgs>();

            _tank = new Entity();
            _tank.AddVehicleTag();
            _tank.AddInteractableTag();
            _tank.AddDamageableTag();
            _tank.AddTransform(tankObject.transform);
            _tank.AddRiggedBody(_body);
            _tank.AddHullVisual(hullVisual.transform);
            _tank.AddHealth(new Health(10, 10));
            _tank.AddDriver(new ReactiveVariable<IEntity>());
            _tank.AddDriveInput(new ReactiveVariable<Vector2>());
            _tank.AddDeathTakenEvent(_death);

            GameObject driverObject = Track(new GameObject("Driver"));
            _driver = new Entity();
            _driver.AddPlayerTag();
            _driver.AddGameObject(driverObject);
            _driver.AddTransform(driverObject.transform);

            _character = new ReactiveVariable<IEntity>(_driver);

            PlayerContext player = MonoEntity.Create<PlayerContext>("Player", installOnAwake: false,
                useUnityLifecycle: false);
            Track(player.gameObject);
            player.AddCharacter(_character);

            _gameContext = MonoEntity.Create<GameContext>("Game", installOnAwake: false, useUnityLifecycle: false);
            Track(_gameContext.gameObject);
            _gameContext.AddPlayerContext(player);
            _gameContext.AddEntityWorld(new EntityWorld<IEntity>(_tank, _driver));

            ControllableUseCase.Enter(_tank, _driver, _character, new[] { _tank, _driver });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _objects)
                if (obj != null)
                    Object.DestroyImmediate(obj);

            _objects.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }

        private TankDestroyBehaviour Install(bool fallOnDeath = false)
        {
            var behaviour = new TankDestroyBehaviour(_gameContext, null, null, fallOnDeath);
            behaviour.Init(_tank);
            return behaviour;
        }

        private void DestroyTank() => _tank.GetHealth().Reduce(10);

        [Test]
        public void Destroyed_DriverIsThrownOutAlive_AndGetsTheControlsBack()
        {
            Install();

            DestroyTank();

            Assert.That(_character.Value, Is.SameAs(_driver));
            Assert.That(_tank.GetDriver().Value, Is.Null);
            Assert.That(_driver.GetGameObject().activeSelf, Is.True);
            Assert.That(_driver.GetTransform().position.y, Is.GreaterThan(Origin.y));
        }

        [Test]
        public void Destroyed_WreckIsNoLongerEnteredOrDamaged_AndReportsItsDeath()
        {
            Install();
            int deaths = 0;
            _death.OnEvent += _ => deaths++;

            DestroyTank();

            Assert.That(_tank.HasInteractableTag(), Is.False);
            Assert.That(_tank.HasDamageableTag(), Is.False);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void TankDestroyed_WreckFreezesWhereItStood()
        {
            Install();
            _body.linearVelocity = new Vector3(4f, 0f, 2f);
            _body.angularVelocity = new Vector3(0f, 1f, 0f);

            DestroyTank();

            Assert.That(_body.isKinematic, Is.True);
            Assert.That(_body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_body.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void HelicopterDestroyed_WreckFallsFreely()
        {
            _body.isKinematic = true;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
            Install(fallOnDeath: true);

            DestroyTank();

            Assert.That(_body.isKinematic, Is.False);
            Assert.That(_body.useGravity, Is.True);
            Assert.That(_body.constraints, Is.EqualTo(RigidbodyConstraints.None));
        }

        [Test]
        public void Destroyed_HullIsBurnt_SharedMaterialKeepsItsColour()
        {
            Assume.That(_hullRenderer.sharedMaterial.HasProperty(ColorId), Is.True, "a shader with _Color");
            Install();

            DestroyTank();

            var block = new MaterialPropertyBlock();
            _hullRenderer.GetPropertyBlock(block);
            Color burnt = block.GetColor(ColorId);
            Assert.That(burnt.r, Is.EqualTo(Paint.r * 0.22f).Within(1e-4f));
            Assert.That(burnt.g, Is.EqualTo(Paint.g * 0.22f).Within(1e-4f));
            Assert.That(burnt.b, Is.EqualTo(Paint.b * 0.22f).Within(1e-4f));
            Assert.That(burnt.a, Is.EqualTo(1f));
            Color shared = _hullRenderer.sharedMaterial.GetColor(ColorId);
            Assert.That(shared.r, Is.EqualTo(Paint.r).Within(1e-4f));
            Assert.That(shared.g, Is.EqualTo(Paint.g).Within(1e-4f));
            Assert.That(shared.b, Is.EqualTo(Paint.b).Within(1e-4f));
        }

        [Test]
        public void DamagedButIntact_NothingHappens()
        {
            Install();

            _tank.GetHealth().Reduce(5);

            Assert.That(_character.Value, Is.SameAs(_tank));
            Assert.That(_tank.HasInteractableTag(), Is.True);
            Assert.That(_body.isKinematic, Is.False);
        }

        [Test]
        public void AfterDispose_DestructionIsIgnored()
        {
            TankDestroyBehaviour behaviour = Install();
            behaviour.Dispose(_tank);

            DestroyTank();

            Assert.That(_character.Value, Is.SameAs(_tank));
            Assert.That(_tank.HasInteractableTag(), Is.True);
        }
    }
}
