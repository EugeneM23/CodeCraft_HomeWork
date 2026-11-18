using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GatePointerUseCaseTests
    {
        private static readonly Vector2 ScreenSize = new(1920, 1080);
        private static readonly Vector2 Centre = new(960, 540);
        private const float MARGIN = 60f;
        private const float EPSILON = 0.01f;

        [Test]
        public void Place_OnScreen_MarksTheGateItself()
        {
            bool onScreen = GatePointerUseCase.Place(new Vector3(800, 400, 10), Centre, Vector2.left, ScreenSize, MARGIN,
                out Vector2 position, out _);

            Assert.IsTrue(onScreen);
            Assert.AreEqual(800f, position.x, EPSILON);
            Assert.AreEqual(400f, position.y, EPSILON);
        }

        [Test]
        public void Place_OffToTheRight_SitsOnTheRightEdgeLevelWithTheTankAndPointsRight()
        {
            bool onScreen = GatePointerUseCase.Place(new Vector3(5000, 300, 10), new Vector2(960, 300), Vector2.right,
                ScreenSize, MARGIN, out Vector2 position, out float angle);

            Assert.IsFalse(onScreen);
            Assert.AreEqual(1920f - MARGIN, position.x, EPSILON);
            Assert.AreEqual(300f, position.y, EPSILON);
            Assert.AreEqual(0f, angle, EPSILON);
        }

        [Test]
        public void Place_Diagonal_StaysInsideTheMarginOnTheRayFromTheTank()
        {
            Vector2 tank = new(700, 500);
            Vector2 direction = new(-1f, 2f);

            GatePointerUseCase.Place(new Vector3(0, 0, 10), tank, direction, ScreenSize, MARGIN, out Vector2 position,
                out float angle);

            Assert.AreEqual(1080f - MARGIN, position.y, EPSILON); // the top edge comes first
            Assert.GreaterOrEqual(position.x, MARGIN);
            Vector2 toPointer = position - tank;
            Assert.AreEqual(0f, direction.x * toPointer.y - direction.y * toPointer.x, 0.5f, "not on the ray from the tank");
            Assert.Greater(angle, 90f);
        }

        [Test]
        public void Place_GateBehindTheCamera_IsNeverOnScreenAndFollowsTheDirection()
        {
            // The projection of a point behind the camera may land inside the screen: the depth decides.
            bool onScreen = GatePointerUseCase.Place(new Vector3(960, 540, -10), Centre, Vector2.down, ScreenSize, MARGIN,
                out Vector2 position, out float angle);

            Assert.IsFalse(onScreen);
            Assert.AreEqual(MARGIN, position.y, EPSILON);
            Assert.AreEqual(-90f, angle, EPSILON);
        }

        [Test]
        public void Place_TankOffScreen_StartsFromTheClampedTank()
        {
            GatePointerUseCase.Place(new Vector3(0, 0, -1), new Vector2(-500, 540), Vector2.right, ScreenSize, MARGIN,
                out Vector2 position, out _);

            Assert.AreEqual(1920f - MARGIN, position.x, EPSILON);
            Assert.AreEqual(540f, position.y, EPSILON);
        }

        [Test]
        public void Place_NoDirection_PointsDown()
        {
            GatePointerUseCase.Place(new Vector3(0, 0, -1), Centre, Vector2.zero, ScreenSize, MARGIN, out Vector2 position,
                out float angle);

            Assert.AreEqual(MARGIN, position.y, EPSILON);
            Assert.AreEqual(-90f, angle, EPSILON);
        }
    }
}
