using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CameraLookUseCaseTests
    {
        private static readonly Vector2 ScreenSize = new(1600f, 900f);
        private const float DeadZone = 0.2f;

        private static readonly CameraLookSettings Settings = new(maxShift: 3f, maxZoom: 0.15f, deadZone: DeadZone);
        private static readonly Vector3 BaseOffset = new(0f, 10f, -6f);

        private static Vector2 Look(float x, float y) =>
            CameraLookUseCase.FromCursor(new Vector2(x, y), ScreenSize, DeadZone);

        private static void AssertNear(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} != {expected}");

        private static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} != {expected}");

        [Test]
        public void CursorInTheCentre_NoLook() => AssertNear(Look(800f, 450f), Vector2.zero);

        [Test]
        public void CursorInsideTheDeadZone_NoLook() =>
            // 0.15 of the half-width right of centre.
            AssertNear(Look(920f, 450f), Vector2.zero);

        [Test]
        public void CursorHalfwayPastTheDeadZone_HalfLook() =>
            // 0.6 of the half-width: (0.6 - 0.2) / (1 - 0.2) = 0.5.
            AssertNear(Look(1280f, 450f), new Vector2(0.5f, 0f));

        [Test]
        public void CursorAtTheTopEdge_FullLookUp() => AssertNear(Look(800f, 900f), new Vector2(0f, 1f));

        [Test]
        public void CursorInTheCorner_ClampedToFull() =>
            AssertNear(Look(1600f, 900f), new Vector2(1f, 1f).normalized);

        [Test]
        public void CursorOutsideTheWindow_SameAsTheEdge() => AssertNear(Look(-500f, 450f), new Vector2(-1f, 0f));

        [Test]
        public void ZeroSizedScreen_NoLook() =>
            AssertNear(CameraLookUseCase.FromCursor(new Vector2(10f, 10f), Vector2.zero, DeadZone), Vector2.zero);

        [Test]
        public void ScreenAxesMapOntoTheGround()
        {
            // Pitched 70° down, no yaw: screen right is +X, screen up is forward, +Z.
            Quaternion rotation = Quaternion.Euler(70f, 0f, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            AssertNear(CameraLookUseCase.ToGround(new Vector2(1f, 0f), right, up), Vector3.right);
            AssertNear(CameraLookUseCase.ToGround(new Vector2(0f, 0.5f), right, up), Vector3.forward * 0.5f);
        }

        [Test]
        public void ScreenAxesFollowTheCameraYaw()
        {
            // Yawed to face +X: screen up is +X, screen right is -Z.
            Quaternion rotation = Quaternion.Euler(70f, 90f, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            AssertNear(CameraLookUseCase.ToGround(new Vector2(0f, 1f), right, up), Vector3.right);
            AssertNear(CameraLookUseCase.ToGround(new Vector2(1f, 0f), right, up), Vector3.back);
        }

        private static Vector3 Forward(float pitch) => Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        [Test]
        public void CameraLookingStraightDown_NoBackwardBoost() =>
            Assert.That(CameraLookUseCase.BackwardBoost(Forward(90f), 40f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void TiltedCamera_BackwardBoostIsTheGroundRatio()
        {
            // The playground camera: 73° down, 40° vertical field of view, about 1.25.
            float expected = Mathf.Sin(93f * Mathf.Deg2Rad) / Mathf.Sin(53f * Mathf.Deg2Rad);

            Assert.That(CameraLookUseCase.BackwardBoost(Forward(73f), 40f), Is.EqualTo(expected).Within(1e-4f));
            Assert.That(expected, Is.EqualTo(1.25f).Within(0.01f));
        }

        [Test]
        public void TopEdgeAtTheHorizon_BackwardBoostCapped() =>
            Assert.That(CameraLookUseCase.BackwardBoost(Forward(30f), 60f), Is.EqualTo(2f).Within(1e-4f));

        [Test]
        public void LookDownTheScreen_ReachesFurther() =>
            AssertNear(CameraLookUseCase.CompensateTilt(new Vector2(0.5f, -0.5f), 1.25f), new Vector2(0.5f, -0.625f));

        [Test]
        public void LookUpTheScreen_Unchanged() =>
            AssertNear(CameraLookUseCase.CompensateTilt(new Vector2(0.5f, 0.5f), 1.25f), new Vector2(0.5f, 0.5f));

        [Test]
        public void BoostedLookBehind_ShiftsFurtherButPullsBackNoMoreThanMax() =>
            AssertNear(
                CameraLookUseCase.Offset(BaseOffset, 1f, Vector3.back * 1.25f, Settings),
                BaseOffset * 1.15f + Vector3.back * 3.75f);

        [Test]
        public void NoLook_OnlyTheDistanceScalesTheOffset() =>
            AssertNear(CameraLookUseCase.Offset(BaseOffset, 1.25f, Vector3.zero, Settings), BaseOffset * 1.25f);

        [Test]
        public void FullLook_ShiftsAndPullsBack() =>
            AssertNear(
                CameraLookUseCase.Offset(BaseOffset, 1f, Vector3.right, Settings),
                BaseOffset * 1.15f + Vector3.right * 3f);

        [Test]
        public void HalfLook_HalfTheShiftAndPullBack() =>
            AssertNear(
                CameraLookUseCase.Offset(BaseOffset, 1f, Vector3.forward * 0.5f, Settings),
                BaseOffset * 1.075f + Vector3.forward * 1.5f);

        [Test]
        public void InAVehicle_ShiftAndPullBackScaleWithDistance() =>
            AssertNear(
                CameraLookUseCase.Offset(BaseOffset, 1.25f, Vector3.right, Settings),
                BaseOffset * (1.25f * 1.15f) + Vector3.right * (3f * 1.25f));
    }
}
