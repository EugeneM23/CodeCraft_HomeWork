using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Step on real agents (handbook §20b.4 p. 6, §18.3, §14.2): one wary step in front of the target, square to it and
    /// facing it, ended where it was headed or by its time; out of the band it goes back in first; with others round
    /// the target it keeps to its own room round its place on the ring, and goes back to its place briskly when that
    /// moved far off — so a crowd waiting with Step and Stand spreads round the target. The steps of Square Off, one
    /// at a time.
    /// </summary>
    public sealed class StepTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Step_Aside_GoesSquareToTheTarget_FacingIt_EndsByItsTime()
        {
            const float RADIUS = 4f;
            GraphRig rig = Alone(new Vector3(RADIUS, 0f, 0f), out Transform target);
            var step = new StepTask { Target = TARGET, Radius = RADIUS, Direction = StepDirection.Aside, Time = 1f, TimeMax = 1f };

            float lasted = 0f, worstOff = 0f;
            bool lookedAway = false;
            TaskStatus status = TaskStatus.Running;
            while (status == TaskStatus.Running && lasted < 5f)
            {
                status = step.Tick(rig.Context, DT);
                lasted += DT;
                if (status != TaskStatus.Running)
                    break;

                Vector3 inward = (target.position - rig.Actuator.Position).normalized;
                worstOff = Mathf.Max(worstOff, Mathf.Abs(Vector3.Angle(rig.Actuator.Body.Velocity, inward) - 90f));
                lookedAway |= Vector3.Angle(rig.Actuator.Facing, target.position - rig.Actuator.Position) > 1f;
                rig.Actuator.Apply(DT);
            }

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            Assert.AreEqual(1f, lasted, DT + 1e-4f, "Alone, nothing but its time ends a sidestep");
            Assert.Less(worstOff, 2f, "Square to the target the whole way");
            Assert.IsFalse(lookedAway, "Its eyes stay on the target");
            Assert.AreEqual(RADIUS, rig.Actuator.Position.magnitude, 0.3f, "Round the target, not off along a line");
        }

        [Test]
        public void Step_InForLong_PacesInAndOutAlongTheLine_WithinItsBand()
        {
            // A step of 4 s at 1.6 m/s is 6.4 m of walking; the band is 2 m wide: the step goes in, then back out, then in
            // again — it lasts its time and never leaves the band
            const float RADIUS = 4f;
            const float BAND = 1f;
            GraphRig rig = Alone(new Vector3(RADIUS, 0f, 0f), out Transform _);
            var step = new StepTask { Target = TARGET, Radius = RADIUS, Band = BAND, Direction = StepDirection.In, Time = 4f, TimeMax = 4f };
            float nearest = float.MaxValue, farthest = 0f, sideways = 0f;
            bool wentInFirst = false;

            float lasted = 0f;
            for (int i = 0; i < 1000 && step.Tick(rig.Context, DT) == TaskStatus.Running; i++)
            {
                rig.Actuator.Apply(DT);
                lasted += DT;
                float distance = rig.Actuator.Position.magnitude;
                if (i == 5)
                    wentInFirst = distance < RADIUS;
                nearest = Mathf.Min(nearest, distance);
                farthest = Mathf.Max(farthest, distance);
                sideways = Mathf.Max(sideways, Mathf.Abs(rig.Actuator.Position.z));
            }

            Assert.AreEqual(4f, lasted, DT * 1.5f, "It lasts its time");
            Assert.IsTrue(wentInFirst, "In first");
            Assert.Less(nearest, RADIUS - 0.3f, "Into the near half of the band…");
            Assert.Greater(farthest, RADIUS + 0.3f, "…and back out into the far half");
            Assert.GreaterOrEqual(nearest, RADIUS - BAND - 0.05f, "Never out of the band");
            Assert.LessOrEqual(farthest, RADIUS + BAND + 0.05f);
            Assert.Less(sideways, 0.05f, "Along the line");
        }

        [Test]
        public void Step_OutOfTheBand_GoesBackIntoItFirst_WhateverItsDirection()
        {
            const float RADIUS = 4f;
            GraphRig rig = Alone(new Vector3(RADIUS + 2f, 0f, 0f), out Transform _);

            // 0.8 s at 1.6 m/s: 1.28 m — all of it on the way back in
            RunToEnd(rig, new StepTask { Target = TARGET, Radius = RADIUS, Band = 1f, Direction = StepDirection.Left, Time = 0.8f, TimeMax = 0.8f });

            Assert.AreEqual(RADIUS + 2f - 1.28f, rig.Actuator.Position.magnitude, 0.1f, "Straight back in, not aside");
            Assert.Less(Mathf.Abs(rig.Actuator.Position.z), 0.05f, "Along the line");
        }

        [Test]
        public void Step_ThreeOnTheRing_EveryStepLastsAtLeastItsTime_PacingWithinItsRoom()
        {
            // The user's numbers (Step Aside 0.4, Time 3–5, Radius 4, Band 2). A step used to end where its first leg was
            // headed — inside the fighter's room round its place, 0.9–1.4 s in — long before its Time
            const float RADIUS = 4f;
            const float TIME = 3f, TIME_MAX = 5f;
            const float WARY = 0.4f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f));             // one fights from the north
            var rigs = new List<GraphRig>
            {
                RigIn(world, AttackSlots.DirectionOf(90f) * RADIUS, target),
                RigIn(world, AttackSlots.DirectionOf(180f) * RADIUS, target),
                RigIn(world, AttackSlots.DirectionOf(270f) * RADIUS, target),
            };
            var step = new StepTask { Target = TARGET, Radius = RADIUS, Band = 2f, Speed = WARY, Time = TIME, TimeMax = TIME_MAX };
            var stand = new StandTask { Target = TARGET, Time = 0.5f };
            var stepping = new bool[rigs.Count];
            var lasted = new List<float>();
            float closest = float.MaxValue;
            int hurried = 0;

            for (int i = 0; i < Mathf.CeilToInt(60f / DT); i++)
            {
                for (int k = 0; k < rigs.Count; k++)
                {
                    GraphRig rig = rigs[k];
                    AgentTask block = stepping[k] ? step : (AgentTask)stand;
                    if (block.Tick(rig.Context, DT) != TaskStatus.Running)
                    {
                        if (stepping[k] && i * DT > 10f)
                            lasted.Add(step.LastLastedFor(rig.Context));
                        stepping[k] = !stepping[k];
                    }

                    if (i * DT > 10f && rig.Actuator.Body.Velocity.magnitude > (WARY + 0.05f) * rig.Actuator.Speed)
                        hurried++;
                    rig.Actuator.Apply(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                for (int a = 0; a < rigs.Count; a++)
                    for (int b = a + 1; b < rigs.Count; b++)
                        closest = Mathf.Min(closest, Vector3.Distance(rigs[a].Actuator.Position, rigs[b].Actuator.Position));
                world.BeginStep(DT);
            }

            Assert.Greater(lasted.Count, 5, "It does take steps");
            foreach (float seconds in lasted)
                Assert.That(seconds, Is.InRange(TIME - DT, TIME_MAX + DT), "Never shorter than Time, never longer than Time Max");
            Assert.AreEqual(0, hurried, "Nobody moved its place: it never hurries back to it");
            Assert.Greater(closest, 1.5f, "Pacing within its room: the neighbours keep their distance");
        }

        [Test]
        public void Step_OffItsPlaceOnTheRing_TakesItAtABriskerStep_OnItsPlaceTheWaryOne()
        {
            const float RADIUS = 5f;
            const float WARY = 0.4f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, RADIUS));           // one attacks from the north
            GraphRig rig = RigIn(world, new Vector3(1f, 0f, RADIUS), target);  // the waiter comes up beside it
            var step = new StepTask { Target = TARGET, Radius = RADIUS, Band = 1f, Speed = WARY, TimeMax = 10f };

            float away = Fastest(rig, step, 10);            // two on the ring: its place is across the target, far off
            AttackRing ring = world.Roster.EngagementOf(target).Ring;
            rig.Actuator.Position = target.position + AttackSlots.DirectionOf(ring.PlaceOf(rig.Agent).Value) * RADIUS;
            rig.Agent.transform.position = rig.Actuator.Position;
            step.Tick(rig.Context, DT);                     // the step back is there: it is over, the next one is wary
            float home = Fastest(rig, step, 10);

            Assert.That(away, Is.EqualTo(BRISK * rig.Actuator.Speed).Within(0.01f), "To its place: briskly, never a run");
            Assert.That(home, Is.EqualTo(WARY * rig.Actuator.Speed).Within(0.01f), "On its place: the wary step");
        }

        [Test]
        public void Step_ThreeOnTheRing_StepAndStandInTurn_KeepToTheirRoom_NeverHurryBack()
        {
            const float RADIUS = 4f;
            const float WARY = 0.4f;
            const float WATCH = 120f;
            const float SETTLE = 10f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f));             // one fights from the north
            var rigs = new List<GraphRig>
            {
                RigIn(world, AttackSlots.DirectionOf(120f) * RADIUS, target),
                RigIn(world, AttackSlots.DirectionOf(240f) * RADIUS, target),
            };
            var step = new StepTask { Target = TARGET, Radius = RADIUS, Band = 1f, Speed = WARY, Time = 2f, TimeMax = 4f };
            var stand = new StandTask { Target = TARGET, Time = 0.5f };      // shorter than the ring forgets an agent
            var stepping = new bool[rigs.Count];
            var walks = new List<float>();
            var walk = new float[rigs.Count];
            int hurried = 0;

            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                for (int k = 0; k < rigs.Count; k++)
                {
                    GraphRig rig = rigs[k];
                    AgentTask block = stepping[k] ? step : (AgentTask)stand;
                    TaskStatus status = block.Tick(rig.Context, DT);
                    bool counting = i * DT > SETTLE;
                    if (counting && rig.Actuator.Body.Velocity.magnitude > (WARY + 0.05f) * rig.Actuator.Speed)
                        hurried++;
                    if (stepping[k])
                        walk[k] += DT;
                    if (status != TaskStatus.Running)
                    {
                        if (stepping[k] && counting)
                            walks.Add(walk[k]);
                        walk[k] = 0f;
                        stepping[k] = !stepping[k];             // Random with two outputs, in turn
                    }

                    rig.Actuator.Apply(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                world.BeginStep(DT);
            }

            Assert.Greater(walks.Count, 10, "It does take steps");
            Assert.AreEqual(0, hurried, "Nobody moved its place: it never hurries back to it");
            Assert.Greater(walks.Average(), 0.8f, "A step is not cut short half a second in");
        }

        [Test]
        public void Step_NineWaiting_SpreadRoundTheTarget_InsteadOfBunchingOnTheSideTheyCameFrom()
        {
            const float RADIUS = 5f;
            const float WATCH = 30f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(1f, 0f, 0f));               // one attacks; the rest wait
            var waiting = new List<GraphRig>();
            for (int i = 0; i < 9; i++)                                        // all of them come from the south-west
                waiting.Add(RigIn(world, new Vector3(-4f - i * 0.4f, 0f, -4f), target));

            var step = new StepTask { Target = TARGET, Radius = RADIUS, Band = 1f };
            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                foreach (GraphRig rig in waiting)
                {
                    step.Tick(rig.Context, DT);                 // a step after a step: each one starts where the last ended
                    rig.Actuator.Apply(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                world.BeginStep(DT);
            }

            var angles = waiting
                .Select(rig => rig.Actuator.Position - target.position)
                .Select(from => Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg)
                .OrderBy(angle => angle).ToList();

            for (int i = 1; i < angles.Count; i++)
                Assert.Greater(angles[i] - angles[i - 1], 15f, "Two of them stand on top of each other: the places are not spread");
            Assert.Greater(angles[angles.Count - 1] - angles[0], 200f, "They are round the target, not all on the side they came from");
        }

        [Test]
        public void Step_RunsIntoAWall_TurnsAwayAtOnce_InsteadOfStandingAgainstIt()
        {
            // An island with obstacles: a step walked into the edge of the NavMesh and the body stood against it until
            // its time was up — the animator played stop and stance in the middle of the node. The wall here: nothing
            // east of x = 4.2 (the body is held at it, as the NavMesh holds an agent at its edge); the step goes out, east
            const float RADIUS = 4f;
            const float WALL = 4.2f;
            const float WARY = 0.4f;
            GraphRig rig = Alone(new Vector3(RADIUS, 0f, 0f), out Transform _);
            var step = new StepTask
            {
                Target = TARGET, Radius = RADIUS, Band = 1f, Direction = StepDirection.Out, Speed = WARY, Time = 10f, TimeMax = 10f
            };
            float pace = WARY * rig.Actuator.Speed;
            float stuck = 0f, longestStuck = 0f;
            int heldByTheWall = 0;
            TaskStatus status = TaskStatus.Running;

            for (int i = 0; i < Mathf.CeilToInt(10f / DT) && status == TaskStatus.Running; i++)
            {
                status = step.Tick(rig.Context, DT);
                Vector3 before = rig.Actuator.Position;
                rig.Actuator.Apply(DT);
                Vector3 at = rig.Actuator.Position;
                if (at.x > WALL)
                {
                    at.x = WALL;
                    heldByTheWall++;
                }

                rig.Actuator.Position = at;
                float rate = (at - before).magnitude / DT;
                stuck = status == TaskStatus.Running && rate < pace * 0.5f ? stuck + DT : 0f;
                longestStuck = Mathf.Max(longestStuck, stuck);
            }

            Assert.Greater(heldByTheWall, 0, "Set-up: the step does reach the wall");
            Assert.Less(longestStuck, 0.5f, "Against the wall it does not stand: the step turns away");
            Assert.Greater(Mathf.Abs(rig.Actuator.Position.z), 0.5f, "…aside, along the wall");
        }

        [Test]
        public void Step_NoTarget_NoTarget()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);

            Assert.AreEqual(TaskStatus.Failure, new StepTask { Target = TARGET }.Tick(rig.Context, DT));
        }

        // ---------- the rig ----------

        /// <summary>The brisk step back to its place, as a part of the body's speed (StepTask.BRISKEST).</summary>
        private const float BRISK = 0.6f;

        private GraphRig Alone(Vector3 at, out Transform target)
        {
            var rig = new GraphRig(trace: false);
            target = rig.CreateTargetAt(Vector3.zero);
            rig.Agent.Blackboard.Set(TARGET, target);
            rig.Actuator.Position = at;
            return rig;
        }

        private static void RunToEnd(GraphRig rig, AgentTask task)
        {
            for (int i = 0; i < 1000; i++)
            {
                if (task.Tick(rig.Context, DT) != TaskStatus.Running)
                    return;

                rig.Actuator.Apply(DT);
            }

            Assert.Fail("The step never ended");
        }

        /// <summary>The fastest the body was told to go over these steps.</summary>
        private static float Fastest(GraphRig rig, AgentTask task, int steps)
        {
            float fastest = 0f;
            for (int i = 0; i < steps; i++)
            {
                task.Tick(rig.Context, DT);
                fastest = Mathf.Max(fastest, rig.Actuator.Body.Velocity.magnitude);
                rig.Actuator.Apply(DT);
                rig.Agent.transform.position = rig.Actuator.Position;
            }

            return fastest;
        }

        private static GraphRig RigIn(IAIWorld world, Vector3 at, Transform target)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, target);
            return rig;
        }

        private Transform NewTarget(Vector3 at, int places)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            go.AddComponent<AttackSlotCapacity>().Slots = places;
            return go.transform;
        }

        // One agent at that place takes the target's attack place, so the rest have to wait
        private static void GivePlaceTo(IAIWorld world, Transform target, Vector3 at)
        {
            GraphRig holder = RigIn(world, at, target);
            var take = new TakeAttackSlotTask { Target = TARGET };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 200 && answer == TaskStatus.Running; i++)
            {
                answer = take.Tick(holder.Context, DT);
                world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer, "Set-up: the place is taken");
        }
    }
}
