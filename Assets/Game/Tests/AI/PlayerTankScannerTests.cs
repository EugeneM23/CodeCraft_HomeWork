using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.AI.Tests
{
    /// <summary>The player's vehicle as the elites see it: found by the scanner, its hull measured and kept out of.</summary>
    public sealed class PlayerTankScannerTests
    {
        private GameObject _tank;
        private Entity _entity;
        private Health _health;
        private PlayerTank _player;
        private PlayerTankScanner _scanner;
        private readonly List<Transform> _found = new();

        [SetUp]
        public void SetUp()
        {
            _tank = new GameObject("Tank") { layer = PhysicsLayers.Character };
            _tank.transform.position = new Vector3(10f, 0f, 0f);
            var box = _tank.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(3.5f, 2f, 6.5f);
            _tank.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;     // a small part: not the hull
            _tank.AddComponent<Rigidbody>().isKinematic = true;

            _entity = new Entity("Tank");
            _entity.AddTransform(_tank.transform);
            _health = new Health(400, 400);
            _entity.AddHealth(_health);
            _player = new PlayerTank();
            _player.Bind(_entity);
            _scanner = new PlayerTankScanner(_player);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tank);

        [Test]
        public void Scan_FindsTheTank_WithinTheRadius()
        {
            int count = _scanner.Scan(new Vector3(-10f, 0f, 0f), 30f, _found);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(_found[0], Is.SameAs(_tank.transform));
        }

        [Test]
        public void Scan_Beyond_FindsNothing()
        {
            Assert.That(_scanner.Scan(new Vector3(-60f, 0f, 0f), 30f, _found), Is.EqualTo(0));
            Assert.That(_found, Is.Empty);
        }

        [Test]
        public void Scan_DeadTank_FindsNothing()
        {
            _health.Reduce(400);

            Assert.That(_scanner.Scan(new Vector3(5f, 0f, 0f), 30f, _found), Is.EqualTo(0));
        }

        [Test]
        public void Scan_NoVehicle_FindsNothing()
        {
            _player.Bind((IEntity)null);

            Assert.That(_scanner.Scan(Vector3.zero, 100f, _found), Is.EqualTo(0));
        }

        [Test]
        public void Scan_DoesNotAllocate()
        {
            _scanner.Scan(new Vector3(-10f, 0f, 0f), 30f, _found);
            Assert.That((TestDelegate)(() => _scanner.Scan(new Vector3(-10f, 0f, 0f), 30f, _found)), NoAllocation.Constraint);
        }

        [Test]
        public void AimPoint_IsTheMiddleOfTheHull()
        {
            Assert.That(_scanner.AimPoint(_tank.transform), Is.EqualTo(new Vector3(10f, 1f, 0f)));
        }

        [Test]
        public void HullDistance_IsToTheBiggestBox_NotToTheMiddle()
        {
            Assert.That(_player.Hull.size.z, Is.EqualTo(6.5f), "the biggest box is the hull");
            Assert.That(_player.DistanceToHull(new Vector3(10f + 1.75f + 1f, 0f, 0f)), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(_player.DistanceToHull(new Vector3(10f, 0f, 3.25f + 2f)), Is.EqualTo(2f).Within(1e-3f));
            Assert.That(_player.DistanceToHull(new Vector3(10f, 0f, 0f)), Is.EqualTo(0f), "inside");
        }

        [Test]
        public void HullDistance_FollowsTheTanksTurn()
        {
            _tank.transform.rotation = Quaternion.Euler(0f, 90f, 0f);    // now 6.5 m long along X

            Assert.That(_player.DistanceToHull(new Vector3(10f + 3.25f + 1f, 0f, 0f)), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void PushOut_PutsABodyAgainstTheNearestSide()
        {
            var inside = new Vector3(11.5f, 0.3f, 0f);
            Assert.IsTrue(_player.PushOut(ref inside, 0.6f));

            Assert.That(inside.x, Is.EqualTo(10f + 1.75f + 0.6f).Within(1e-3f), "out of the near (right) side");
            Assert.That(inside.y, Is.EqualTo(0.3f), "its height is its own");

            var outside = new Vector3(15f, 0f, 0f);
            Assert.IsFalse(_player.PushOut(ref outside, 0.6f));
        }
    }
}
