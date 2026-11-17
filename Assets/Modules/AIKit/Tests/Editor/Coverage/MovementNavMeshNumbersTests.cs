using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The NavMesh body's numbers with one owner each (step 90 recheck; no NavMesh needed): the pace and the full speed of
    /// the pathing, the turn speed, acceleration and ramp distance of the mover, the avoidance priority it gives way with
    /// while inside, and where a body may be put down without the crowd shoving it.
    /// </summary>
    public sealed class MovementNavMeshNumbersTests
    {
        private readonly List<GameObject> _made = new();

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;      // a NavMeshAgent without a baked mesh complains

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        private NavMeshAgent Make(NavMeshBody body, string name = "Character", bool active = false, float speed = 4f)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.SetActive(active);
            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.radius = 0.4f;
            body.Attach(go.transform);
            return agent;
        }

        [Test]
        public void NavMeshPathing_PaceAndRunSpeed_OneOwner()
        {
            var body = new NavMeshBody { RampDistance = 0.5f };
            NavMeshAgent agent = Make(body);
            var numbers = (IAgentNumbers)body.Pathing;

            body.Pathing.Pace = 0.5f;
            Assert.AreEqual(2f, agent.speed, 1e-4f, "Half the full speed");
            Assert.AreEqual(2f * 2f / (2f * 0.5f), agent.acceleration, 1e-3f, "Up to it over the ramp: v²/2s");

            numbers.SetNumber(0, 6f);
            Assert.AreEqual(3f, agent.speed, 1e-4f, "A new full speed, walked at the same pace");
            Assert.AreEqual(6f, numbers.GetNumber(0), 1e-4f, "The full speed is what is read back");

            body.Pathing.Pace = 1f;
            Assert.AreEqual(6f, agent.speed, 1e-4f);
            Assert.AreEqual(36f, agent.acceleration, 1e-3f);

            body.Pathing.Pace = 0f;
            Assert.AreEqual(0.05f, body.Pathing.Pace, 1e-6f, "Never quite standing");
            Assert.AreEqual(0.3f, agent.speed, 1e-4f);
        }

        [Test]
        public void NavMeshMover_SetNumber_RampDistanceTurnSpeedAcceleration()
        {
            var body = new NavMeshBody { RampDistance = 0.5f };
            NavMeshAgent agent = Make(body);
            body.ApplyAcceleration(agent);
            var numbers = (IAgentNumbers)body.Mover;
            var names = new List<string>();
            numbers.NamesOf(names);
            CollectionAssert.AreEqual(new[] { NavMeshMover.TURN_SPEED, NavMeshMover.ACCELERATION, NavMeshMover.RAMP_DISTANCE }, names);

            Assert.AreEqual(16f, numbers.GetNumber(1), 1e-3f, "4 m/s over half a metre: 16 m/s²");
            numbers.SetNumber(2, 1f);
            Assert.AreEqual(1f, numbers.GetNumber(2), 1e-6f);
            Assert.AreEqual(8f, agent.acceleration, 1e-3f, "Twice the ramp: half the acceleration");

            numbers.SetNumber(2, 0f);
            numbers.SetNumber(1, 5f);
            Assert.AreEqual(5f, agent.acceleration, 1e-6f, "No ramp: the acceleration as it is set");
            Assert.AreEqual(5f, numbers.GetNumber(1), 1e-6f);

            numbers.SetNumber(0, -3f);
            Assert.AreEqual(0f, numbers.GetNumber(0), 1e-6f, "No turning backwards");
            Assert.AreEqual(0f, agent.angularSpeed, 1e-6f);
        }

        [Test]
        public void NavMeshBody_SetInside_LeastImportantWhileInside_ItsOwnPriorityBack()
        {
            var body = new NavMeshBody { OwnAvoidancePriority = true };
            NavMeshAgent agent = Make(body, "Villager 3");
            int own = NavMeshBody.PriorityFor("Villager 3");
            Assert.AreEqual(own, agent.avoidancePriority, "Set-up: its own priority");

            body.SetInside(true);
            body.SetInside(true);
            Assert.AreEqual(99, agent.avoidancePriority, "Inside: nobody walks round it");

            body.SetInside(false);
            Assert.AreEqual(own, agent.avoidancePriority, "Out: its own again (not the 99 of the second ask)");

            agent.avoidancePriority = 12;
            body.SetInside(false);
            Assert.AreEqual(12, agent.avoidancePriority, "Out already: nothing changes");
        }

        [Test]
        public void NavMeshMover_IsFree_ABodyNearerThanTheRadii_NotFree_ItselfAndSwitchedOffIgnored()
        {
            var mine = new NavMeshBody();
            NavMeshAgent self = Make(mine, "Self", active: true);
            self.transform.position = new Vector3(5f, 0f, 5f);
            var theirs = new NavMeshBody();
            NavMeshAgent other = Make(theirs, "Other", active: true);
            other.transform.position = Vector3.zero;

            Assert.IsFalse(NavMeshMover.IsFree(new Vector3(0.5f, 0f, 0f), 0.4f, self), "Nearer than the two radii");
            Assert.IsTrue(NavMeshMover.IsFree(new Vector3(1f, 0f, 0f), 0.4f, self), "Farther than the two radii");
            Assert.IsTrue(NavMeshMover.IsFree(Vector3.zero, 0.4f, other), "Its own spot does not count against it");

            other.gameObject.SetActive(false);
            Assert.IsTrue(NavMeshMover.IsFree(new Vector3(0.5f, 0f, 0f), 0.4f, self), "A body switched off is nowhere");
        }
    }
}
