using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MountDeathBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4800f, 300f, 4800f);

        private readonly List<GameObject> _objects = new();

        private IEntity _mount;
        private IEntity _rider;
        private IEntity _chaser;
        private ReactiveVariable<IEntity> _character;
        private GameContext _gameContext;

        [SetUp]
        public void SetUp()
        {
            GameObject mountObject = Track(new GameObject("Mount"));
            mountObject.transform.position = Origin;

            _mount = new Entity();
            _mount.AddVehicleTag();
            _mount.AddInteractableTag();
            _mount.AddTransform(mountObject.transform);
            _mount.AddHealth(new Health(10, 10));
            _mount.AddDriver(new ReactiveVariable<IEntity>());
            _mount.AddMoveDirection(new ReactiveVariable<Vector3>());

            GameObject riderObject = Track(new GameObject("Rider"));
            _rider = new Entity();
            _rider.AddPlayerTag();
            _rider.AddGameObject(riderObject);
            _rider.AddTransform(riderObject.transform);

            _character = new ReactiveVariable<IEntity>(_rider);

            PlayerContext player = MonoEntity.Create<PlayerContext>("Player", installOnAwake: false,
                useUnityLifecycle: false);
            Track(player.gameObject);
            player.AddCharacter(_character);

            _chaser = new Entity();
            _chaser.AddEnemyTag();
            _chaser.AddTarget(new ReactiveVariable<IEntity>());

            _gameContext = MonoEntity.Create<GameContext>("Game", installOnAwake: false, useUnityLifecycle: false);
            Track(_gameContext.gameObject);
            _gameContext.AddPlayerContext(player);
            _gameContext.AddEntityWorld(new EntityWorld<IEntity>(_mount, _rider, _chaser));

            ControllableUseCase.Enter(_mount, _rider, _character, new[] { _mount, _rider });
            _chaser.GetTarget().Value = _mount;
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

        private MountDeathBehaviour Install()
        {
            var behaviour = new MountDeathBehaviour(_gameContext);
            behaviour.Init(_mount);
            return behaviour;
        }

        [Test]
        public void MountDies_RiderIsThrownOffAlive_WithTheControlsAndTheEnemiesBack()
        {
            Install();

            _mount.GetHealth().Reduce(10);

            Assert.That(_character.Value, Is.SameAs(_rider));
            Assert.That(_mount.GetDriver().Value, Is.Null);
            Assert.That(_rider.GetGameObject().activeSelf, Is.True);
            Assert.That(_chaser.GetTarget().Value, Is.SameAs(_rider));
        }

        [Test]
        public void MountDies_NobodyGetsBackOn()
        {
            Install();

            _mount.GetHealth().Reduce(10);

            Assert.That(_mount.HasInteractableTag(), Is.False);
        }

        [Test]
        public void MountHurt_RiderStaysOn()
        {
            Install();

            _mount.GetHealth().Reduce(5);

            Assert.That(_character.Value, Is.SameAs(_mount));
            Assert.That(_mount.HasInteractableTag(), Is.True);
        }

        [Test]
        public void AfterDispose_DeathIsIgnored()
        {
            Install().Dispose(_mount);

            _mount.GetHealth().Reduce(10);

            Assert.That(_character.Value, Is.SameAs(_mount));
        }
    }
}
