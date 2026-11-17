using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HelicopterGunAimBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4300f, 300f, 4300f);

        /// <summary>Fast enough that one tick of a second reaches any target.</summary>
        private const float SNAP = 10000f;

        private const float MAX_YAW = 30f;
        private const float MAX_PITCH_DOWN = 60f;

        private GameObject _root;
        private Transform _leftPivot;
        private Transform _rightPivot;
        private Transform _leftVisual;
        private IEntity _helicopter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Helicopter");
            _root.transform.position = Origin;

            _leftPivot = Child(_root.transform, "LeftPivot", new Vector3(-1f, 0f, 0f));
            _rightPivot = Child(_root.transform, "RightPivot", new Vector3(1f, 0f, 0f));

            Transform body = Child(_root.transform, "Body", Vector3.zero);
            _leftVisual = Child(body, "LeftGun", new Vector3(-1f, 0f, 0f));

            _helicopter = new Entity();
            _helicopter.AddTransform(_root.transform);
            _helicopter.AddAimPoint(new ReactiveVariable<Vector3>());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private HelicopterGunAimBehaviour CreateAim(float speed = SNAP)
        {
            // One visual for two pivots: the right gun has none.
            var aim = new HelicopterGunAimBehaviour(new[] { _leftPivot, _rightPivot }, new[] { _leftVisual }, speed,
                MAX_YAW, MAX_PITCH_DOWN);
            aim.Init(_helicopter);
            return aim;
        }

        private void AimAt(Vector3 point) => _helicopter.GetAimPoint().Value = point;

        [Test]
        public void BothGunsConvergeOnTheCursor_FromTheirOwnMounts()
        {
            _root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 target = Origin + _root.transform.forward * 8f + Vector3.down * 6f;
            AimAt(target);

            CreateAim().Tick(_helicopter, 1f);

            foreach (Transform pivot in new[] { _leftPivot, _rightPivot })
            {
                Vector3 toTarget = (target - pivot.position).normalized;
                Assert.That(Vector3.Angle(pivot.forward, toTarget), Is.LessThan(0.05f), pivot.name);
            }
        }

        [Test]
        public void VisibleGun_CopiesItsPivotsTurn()
        {
            AimAt(_leftPivot.position + new Vector3(2f, -5f, 5f));

            CreateAim().Tick(_helicopter, 1f);

            Assert.That(Quaternion.Angle(_leftVisual.localRotation, _leftPivot.localRotation), Is.LessThan(1e-3f));
            Assert.That(Quaternion.Angle(_leftPivot.localRotation, Quaternion.identity), Is.GreaterThan(1f));
        }

        [Test]
        public void FarToTheSide_YawStopsAtItsLimit()
        {
            AimAt(_rightPivot.position + new Vector3(5f, -5f, 0f));

            CreateAim().Tick(_helicopter, 1f);

            Assert.That(Quaternion.Angle(_rightPivot.localRotation, Quaternion.Euler(45f, MAX_YAW, 0f)),
                Is.LessThan(0.05f));
        }

        [Test]
        public void TargetAbove_GunStaysLevel_NeverAimsUp()
        {
            AimAt(_leftPivot.position + new Vector3(0f, 5f, 5f));

            CreateAim().Tick(_helicopter, 1f);

            Assert.That(Quaternion.Angle(_leftPivot.localRotation, Quaternion.identity), Is.LessThan(0.05f));
        }

        [Test]
        public void TurnsNoFasterThanItsSpeed()
        {
            AimAt(_leftPivot.position + new Vector3(0f, -5f, 5f));

            CreateAim(speed: 10f).Tick(_helicopter, 0.5f);

            Assert.That(Quaternion.Angle(_leftPivot.localRotation, Quaternion.identity), Is.EqualTo(5f).Within(0.05f));
        }
    }
}
