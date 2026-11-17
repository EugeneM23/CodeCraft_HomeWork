using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static AIKit.Tests.MovementCoverageRigs;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The ring of places round a target as the approach blocks share it (step 90 recheck; handbook §18.3, §14.1 —
    /// hysteresis, not a new share every step): before anybody holds a place to attack from, the first to come anchors
    /// the ring and keeps it — the places are not dealt anew on every step, and nothing is allocated for it. A flanker with
    /// no way round to its place goes straight in.
    /// </summary>
    public sealed class MovementRingTests
    {
        private const float DT = 0.02f;
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Flank_TwoComeAndNobodyHoldsAPlace_PlacesDoNotFlip_NothingAllocated()
        {
            // Both come before anybody holds a place: each of them is "first", and each anchored the ring in turn — the
            // ring was dealt anew at every step of every one of them (a closure for its sort each time)
            var world = new AIWorld();
            Transform target = NewTarget(_made, Vector3.zero, 2);
            GraphRig a = RigIn(world, new Vector3(-4f, 0f, 0f), target);
            GraphRig b = RigIn(world, new Vector3(4f, 0f, -3f), target);
            var flank = new FlankTask { Target = TARGET, StopDistance = 1.5f, BypassRadius = 3f };
            AttackRing ring = world.Roster.EngagementOf(target).Ring;
            var places = new List<float>(32);
            int allocated = 0;

            for (int tick = 0; tick < 10; tick++)
            {
                float afterA = float.NaN, afterB = float.NaN;
                int made = Allocations.Count(() =>
                {
                    flank.Tick(a.Context, DT);
                    afterA = ring.PlaceOf(a.Agent) ?? float.NaN;
                    flank.Tick(b.Context, DT);
                    afterB = ring.PlaceOf(a.Agent) ?? float.NaN;
                });
                places.Add(afterA);
                places.Add(afterB);
                if (tick >= 2)
                    allocated += made;

                Apply(a, DT);
                Apply(b, DT);
                world.BeginStep(DT);
            }

            for (int i = 4; i < places.Count; i++)
            {
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(places[3], places[i])), Is.LessThan(1f),
                    $"A's place stays where it is (read {i}: {places[i]:0.0}°, was {places[3]:0.0}°)");
            }

            Assert.AreEqual(0, allocated, "Asking for places allocates nothing");
        }

        // Refuses every point farther than Reach from the target: no way round, only straight in
        private sealed class OnlyStraightIn : INavigator
        {
            private readonly DirectNavigator _way;
            private readonly Transform _target;

            public OnlyStraightIn(IAgentActuator body, Transform target)
            {
                _way = new DirectNavigator(body);
                _target = target;
            }

            public float Reach = 1f;

            public NavVerdict SetDestination(Vector3 point)
            {
                if (Flat(point - _target.position).magnitude > Reach)
                {
                    _way.Clear();
                    return NavVerdict.Unreachable;
                }

                return _way.SetDestination(point);
            }

            public void Clear() => _way.Clear();

            public NavStatus Status => _way.Status;

            public Vector3 Destination => _way.Destination;

            public Vector3 SteeringTarget => _way.SteeringTarget;

            public float RemainingDistance => _way.RemainingDistance;

            public NavEdgeKind CurrentEdge => _way.CurrentEdge;

            public void Step(float dt) => _way.Step(dt);
        }

        [Test]
        public void Flank_NoWayRoundToItsPlace_GoesStraight_InPlaceAtStopDistance()
        {
            const float STOP = 1.5f;
            var world = new AIWorld();
            Transform target = NewTarget(_made, Vector3.zero, 2);
            GivePlaceTo(world, target, new Vector3(-2f, 0f, 0f), DT);               // the holder anchors the ring west
            var body = new TestActuator { Position = new Vector3(-4f, 0f, 3f) };
            var rig = new GraphRig(trace: false, world: world, navigator: new OnlyStraightIn(body, target), actuator: body)
                { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Agent.transform.position = body.Position;
            rig.Agent.Blackboard.Set(TARGET, target);
            var flank = new FlankTask { Target = TARGET, StopDistance = STOP, BypassRadius = 3f };

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 400 && status == TaskStatus.Running; i++)
            {
                status = flank.Tick(rig.Context, DT);
                Apply(rig, DT);
                world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, status, "No way round its side: it goes straight in — In Place");
            Assert.That(Flat(rig.Actuator.Position - target.position).magnitude, Is.EqualTo(STOP).Within(0.35f),
                "At Stop Distance");
        }
    }
}
