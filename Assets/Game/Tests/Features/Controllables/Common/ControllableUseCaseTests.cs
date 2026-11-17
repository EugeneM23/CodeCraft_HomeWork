using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ControllableUseCaseTests
    {
        // Far from anything a scene open in the editor might hold, so the exit checks only meet
        // the colliders a test puts there itself.
        private static readonly Vector3 Origin = new(1000f, 1000f, 1000f);

        private readonly List<GameObject> _objects = new();

        private IEntity _vehicle;
        private IEntity _driver;
        private Transform _leftExit;
        private Transform _rightExit;
        private ReactiveVariable<IEntity> _character;
        private List<IEntity> _world;

        [SetUp]
        public void SetUp()
        {
            GameObject vehicleObject = Track(new GameObject("Vehicle"));
            vehicleObject.transform.position = Origin;

            _leftExit = Track(new GameObject("ExitLeft")).transform;
            _leftExit.SetParent(vehicleObject.transform, false);
            _leftExit.localPosition = new Vector3(-3f, 0f, 0f);

            _rightExit = Track(new GameObject("ExitRight")).transform;
            _rightExit.SetParent(vehicleObject.transform, false);
            _rightExit.localPosition = new Vector3(3f, 0f, 0f);

            _vehicle = new Entity();
            _vehicle.AddVehicleTag();
            _vehicle.AddTransform(vehicleObject.transform);
            _vehicle.AddHealth(new Health(10, 10));
            _vehicle.AddDriver(new ReactiveVariable<IEntity>());
            _vehicle.AddDriveInput(new ReactiveVariable<Vector2>());
            _vehicle.AddExitPoints(new[] { _leftExit, _rightExit });

            GameObject driverObject = Track(new GameObject("Driver"));
            driverObject.transform.position = Origin + new Vector3(0f, 0f, -5f);

            _driver = new Entity();
            _driver.AddPlayerTag();
            _driver.AddGameObject(driverObject);
            _driver.AddTransform(driverObject.transform);
            _driver.AddTargetInteractable(new ReactiveVariable<IEntity>(_vehicle));

            _character = new ReactiveVariable<IEntity>(_driver);
            _world = new List<IEntity> { _vehicle, _driver };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private IEntity AddEnemy(IEntity target)
        {
            IEntity enemy = new Entity();
            enemy.AddEnemyTag();
            enemy.AddTarget(new ReactiveVariable<IEntity>(target));
            _world.Add(enemy);
            return enemy;
        }

        private void Block(Transform point)
        {
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = point.position + Vector3.up;
            wall.transform.localScale = new Vector3(2f, 2f, 2f);
        }

        private void EnterVehicle() => ControllableUseCase.Enter(_vehicle, _driver, _character, _world);

        [Test]
        public void Enter_HandsCharacterToVehicle()
        {
            EnterVehicle();

            Assert.That(_character.Value, Is.SameAs(_vehicle));
            Assert.That(_vehicle.GetDriver().Value, Is.SameAs(_driver));
            Assert.That(_vehicle.HasPlayerTag(), Is.True);
            Assert.That(_driver.GetGameObject().activeSelf, Is.False);
            Assert.That(_driver.GetTargetInteractable().Value, Is.Null);
        }

        [Test]
        public void Enter_RetargetsEnemiesChasingDriver()
        {
            IEntity chaser = AddEnemy(_driver);
            IEntity other = new Entity();
            IEntity bystander = AddEnemy(other);

            EnterVehicle();

            Assert.That(chaser.GetTarget().Value, Is.SameAs(_vehicle));
            Assert.That(bystander.GetTarget().Value, Is.SameAs(other));
        }

        [Test]
        public void CanEnter_OnlyWhenEmptyAndIntact()
        {
            Assert.That(ControllableUseCase.CanEnter(_vehicle, _driver), Is.True);

            EnterVehicle();
            Assert.That(ControllableUseCase.CanEnter(_vehicle, _driver), Is.False, "occupied");

            ControllableUseCase.Exit(_vehicle, _leftExit.position, _character, _world);
            _vehicle.GetHealth().Reduce(10);
            Assert.That(ControllableUseCase.CanEnter(_vehicle, _driver), Is.False, "destroyed");
        }

        [Test]
        public void CanEnter_RejectsNonPlayers()
        {
            IEntity stranger = new Entity();

            Assert.That(ControllableUseCase.CanEnter(_vehicle, stranger), Is.False);
        }

        [Test]
        public void Exit_GivesCharacterBack()
        {
            EnterVehicle();
            IEntity chaser = AddEnemy(_vehicle);
            _vehicle.GetDriveInput().Value = new Vector2(1f, 1f);

            ControllableUseCase.Exit(_vehicle, _rightExit.position, _character, _world);

            Assert.That(_character.Value, Is.SameAs(_driver));
            Assert.That(_vehicle.GetDriver().Value, Is.Null);
            Assert.That(_vehicle.HasPlayerTag(), Is.False);
            Assert.That(_vehicle.GetDriveInput().Value, Is.EqualTo(Vector2.zero));
            Assert.That(_driver.GetGameObject().activeSelf, Is.True);
            Assert.That(_driver.GetTransform().position, Is.EqualTo(_rightExit.position));
            Assert.That(chaser.GetTarget().Value, Is.SameAs(_driver));
        }

        [Test]
        public void TryExit_FirstFreePointWins()
        {
            EnterVehicle();
            Block(_leftExit);
            Physics.SyncTransforms();

            bool exited = ControllableUseCase.TryExit(_vehicle, _character, _world);

            Assert.That(exited, Is.True);
            Assert.That(_driver.GetTransform().position, Is.EqualTo(_rightExit.position));
        }

        [Test]
        public void TryExit_AllPointsBlocked_KeepsDriverInside()
        {
            EnterVehicle();
            Block(_leftExit);
            Block(_rightExit);
            Physics.SyncTransforms();

            bool exited = ControllableUseCase.TryExit(_vehicle, _character, _world);

            Assert.That(exited, Is.False);
            Assert.That(_character.Value, Is.SameAs(_vehicle));
            Assert.That(_vehicle.GetDriver().Value, Is.SameAs(_driver));
            Assert.That(_driver.GetGameObject().activeSelf, Is.False);
        }

        [Test]
        public void Eject_AllPointsBlocked_PutsDriverAboveVehicle()
        {
            EnterVehicle();
            Block(_leftExit);
            Block(_rightExit);
            Physics.SyncTransforms();

            ControllableUseCase.Eject(_vehicle, _character, _world);

            Assert.That(_character.Value, Is.SameAs(_driver));
            Assert.That(_driver.GetTransform().position.y, Is.GreaterThan(Origin.y + 1f));
        }

        [Test]
        public void Eject_EmptyVehicle_DoesNothing()
        {
            ControllableUseCase.Eject(_vehicle, _character, _world);

            Assert.That(_character.Value, Is.SameAs(_driver));
        }
    }
}
