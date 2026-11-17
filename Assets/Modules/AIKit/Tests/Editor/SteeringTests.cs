using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 5 steering without a scene: the fallback navigator turns no faster than its limit, turns at once without
    /// one, and stops before a pit.
    /// </summary>
    public class SteeringTests
    {
        private sealed class FlatGroundUntil : IGroundProbe
        {
            public float EndX = float.MaxValue;

            public bool FindGround(Vector3 point, Vector3 up, float maxDepth, out Vector3 ground)
            {
                ground = new Vector3(point.x, 0f, point.z);
                return point.x < EndX;
            }
        }

        private const float DT = 0.02f;

        private static Vector3 StepAndMeasure(DirectNavigator navigator, TestActuator body)
        {
            Vector3 before = body.Position;
            navigator.Step(DT);
            body.Apply(DT);
            return body.Position - before;
        }

        [Test]
        public void DirectNavigator_Reversal_TurnsNoFasterThanMaxTurnSpeed()
        {
            var body = new TestActuator();
            var navigator = new DirectNavigator(body) { MaxTurnSpeed = 300f };
            navigator.SetDestination(new Vector3(10f, 0f, 0f));
            StepAndMeasure(navigator, body);

            navigator.SetDestination(new Vector3(-10f, 0f, 0f));
            Vector3 moved = StepAndMeasure(navigator, body);

            Assert.LessOrEqual(Vector3.Angle(Vector3.right, moved), 300f * DT + 0.01f, "Turned faster than the limit");
            Assert.AreEqual(0f, moved.y, 1e-5f, "A walker never turns into its up axis");
        }

        [Test]
        public void DirectNavigator_Reversal_FacesTheWayWithinTheTurnTime()
        {
            var body = new TestActuator();
            var navigator = new DirectNavigator(body) { MaxTurnSpeed = 300f };
            navigator.SetDestination(new Vector3(10f, 0f, 0f));
            StepAndMeasure(navigator, body);
            navigator.SetDestination(new Vector3(-10f, 0f, 0f));

            Vector3 moved = Vector3.zero;
            for (int i = 0; i < Mathf.CeilToInt(180f / 300f / DT) + 1; i++)
                moved = StepAndMeasure(navigator, body);

            Vector3 toDestination = new Vector3(-10f, 0f, 0f) - body.Position;
            Assert.Less(Vector3.Angle(toDestination, moved), 1f, "Turned round in 180° / MaxTurnSpeed");
        }

        [Test]
        public void DirectNavigator_NoLimit_TurnsAtOnce()
        {
            var body = new TestActuator();
            var navigator = new DirectNavigator(body) { MaxTurnSpeed = 0f };
            navigator.SetDestination(new Vector3(10f, 0f, 0f));
            StepAndMeasure(navigator, body);

            navigator.SetDestination(new Vector3(-10f, 0f, 0f));

            Assert.Less(Vector3.Angle(Vector3.left, StepAndMeasure(navigator, body)), 0.01f);
        }

        [Test]
        public void DirectNavigator_PitAhead_StandsBlocked()
        {
            var body = new TestActuator { Position = new Vector3(0.5f, 0f, 0f) };
            var navigator = new DirectNavigator(body, new FlatGroundUntil { EndX = 1f });
            navigator.SetDestination(new Vector3(10f, 0f, 0f));

            Vector3 moved = StepAndMeasure(navigator, body);

            Assert.AreEqual(NavStatus.Blocked, navigator.Status);
            Assert.AreEqual(Vector3.zero, moved, "Does not step into the pit");
            Assert.IsTrue(navigator.Status.IsMoving(), "Blocked is not giving up");
        }

        [Test]
        public void DirectNavigator_GroundGoesOn_Follows()
        {
            var body = new TestActuator();
            var navigator = new DirectNavigator(body, new FlatGroundUntil());
            navigator.SetDestination(new Vector3(10f, 0f, 0f));

            Assert.Greater(StepAndMeasure(navigator, body).x, 0f);
            Assert.AreEqual(NavStatus.Following, navigator.Status);
        }
    }
}
