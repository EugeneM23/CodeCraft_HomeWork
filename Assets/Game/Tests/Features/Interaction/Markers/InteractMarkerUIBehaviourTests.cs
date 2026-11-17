using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class InteractMarkerUIBehaviourTests
    {
        private static readonly Vector3 MarkerOffset = new(0f, 1.5f, 0f);

        private readonly List<GameObject> _objects = new();

        private GameObject _view;
        private Transform _camera;
        private IReactiveVariable<IEntity> _target;
        private IEntity _marker;
        private InteractMarkerUIBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _view = NewObject("Marker", Vector3.zero);
            _camera = NewObject("Camera", new Vector3(5f, 10f, 0f)).transform;

            _target = new ReactiveVariable<IEntity>();
            IEntity character = new Entity();
            character.AddTargetInteractable(_target);

            IEntity camera = new Entity();
            camera.AddTransform(_camera);

            PlayerContext player = MonoEntity.Create<PlayerContext>(name: "PlayerContext");
            _objects.Add(player.gameObject);
            player.AddCharacter(new ReactiveVariable<IEntity>(character));
            player.AddCamera(camera);

            GameContext context = MonoEntity.Create<GameContext>(name: "GameContext");
            _objects.Add(context.gameObject);
            context.AddPlayerContext(player);

            _marker = new Entity();
            _marker.AddPositionOffset(MarkerOffset);
            _behaviour = new InteractMarkerUIBehaviour(_view, context);
            _behaviour.Init(_marker);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private GameObject NewObject(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            _objects.Add(gameObject);
            return gameObject;
        }

        private IEntity Item(Vector3 position)
        {
            IEntity item = new Entity();
            item.AddTransform(NewObject("Item", position).transform);
            return item;
        }

        [Test]
        public void ItemTargeted_ShowsTheMarkerAboveIt()
        {
            _target.Value = Item(new Vector3(3f, 0f, 4f));

            Assert.That(_view.activeSelf, Is.True);
            Assert.That(Vector3.Distance(_view.transform.position, new Vector3(3f, 1.5f, 4f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TallItem_UsesItsOwnOffset()
        {
            IEntity tank = Item(new Vector3(3f, 0f, 4f));
            tank.AddPositionOffset(new Vector3(0f, 4f, 0f));

            _target.Value = tank;

            Assert.That(Vector3.Distance(_view.transform.position, new Vector3(3f, 4f, 4f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TargetLost_HidesTheMarker()
        {
            _target.Value = Item(Vector3.zero);

            _target.Value = null;

            Assert.That(_view.activeSelf, Is.False);
        }

        [Test]
        public void LateTick_TurnsTheMarkerAwayFromTheCamera_OnlyAboutTheVertical()
        {
            _target.Value = Item(new Vector3(0f, -1.5f, 0f));

            _behaviour.LateTick(_marker, 0.02f);

            // The camera is above and to the east: the marker faces west and stays upright.
            Assert.That(Vector3.Distance(_view.transform.forward, Vector3.left), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(_view.transform.up, Vector3.up), Is.LessThan(1e-4f));
        }

        [Test]
        public void LateTick_HiddenMarker_IsNotTurned()
        {
            _view.SetActive(false);

            _behaviour.LateTick(_marker, 0.02f);

            Assert.That(_view.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void AfterDispose_TargetChangesAreIgnored()
        {
            _view.SetActive(false);
            _behaviour.Dispose(_marker);

            _target.Value = Item(Vector3.zero);

            Assert.That(_view.activeSelf, Is.False);
        }
    }
}
