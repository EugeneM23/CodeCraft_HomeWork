using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TurretAimBehaviourTests
    {
        private const float SPEED = 90f;

        private GameObject _hull;
        private Transform _pivot;
        private Transform _visual;
        private IEntity _tank;
        private TurretAimBehaviour _aim;
        private ReactiveVariable<float> _speed;

        [SetUp]
        public void SetUp()
        {
            _hull = new GameObject("Tank");

            _pivot = new GameObject("TurretPivot").transform;
            _pivot.SetParent(_hull.transform, false);

            Transform hullVisual = new GameObject("HullVisual").transform;
            hullVisual.SetParent(_hull.transform, false);
            _visual = new GameObject("Turret").transform;
            _visual.SetParent(hullVisual, false);

            _tank = new Entity();
            _tank.AddRotateDirection(new ReactiveVariable<Vector3>());
            _speed = new ReactiveVariable<float>(SPEED);
            _tank.AddTurretRotationSpeed(_speed);
            _tank.AddTurretPivot(_pivot);
            _tank.AddTurretVisual(_visual);

            _aim = new TurretAimBehaviour();
            _aim.Init(_tank);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hull);

        private void AimAlong(Vector3 direction) => _tank.GetRotateDirection().Value = direction;

        private static float Angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);

        [Test]
        public void TurnsTowardsTheAim_AtItsSpeed()
        {
            AimAlong(Vector3.right);

            _aim.Tick(_tank, 0.5f);

            Assert.That(Angle(_pivot.rotation, Quaternion.Euler(0f, SPEED * 0.5f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void ReachesTheAim_AndHoldsIt()
        {
            AimAlong(Vector3.right);

            _aim.Tick(_tank, 2f);
            _aim.Tick(_tank, 2f);

            Assert.That(Angle(_pivot.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void AimUpOrDown_PivotOnlyYaws()
        {
            AimAlong(new Vector3(1f, -3f, 0f));

            _aim.Tick(_tank, 2f);

            Assert.That(Angle(_pivot.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void NoAim_TurretStaysPut()
        {
            _pivot.rotation = Quaternion.Euler(0f, 40f, 0f);

            _aim.Tick(_tank, 1f);

            Assert.That(Angle(_pivot.rotation, Quaternion.Euler(0f, 40f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void HullTurnsUnderneath_TurretHoldsItsAim_VisualFollowsThePivot()
        {
            _speed.Value = 1000f;
            AimAlong(Vector3.right);
            _aim.Tick(_tank, 1f);

            _hull.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            _aim.Tick(_tank, 1f);

            Assert.That(Angle(_pivot.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            Assert.That(Angle(_visual.localRotation, Quaternion.Euler(0f, 60f, 0f)), Is.LessThan(0.01f));
        }
    }
}
