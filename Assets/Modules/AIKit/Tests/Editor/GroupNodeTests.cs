using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The places a target gives to attack it from, and the group blocks that share them: Take Attack Slot asks and
    /// answers Got or Busy (a clearly nearer one takes the farthest holder's place), Release Attack Slot gives a place
    /// back, Circle Target goes round at its radius and waits for a place all the while, and the target's Attack Slots
    /// say how many places there are. Real agents share one world; the group answers between their steps, as the
    /// world's step comes before the agents' in the game.
    /// </summary>
    public sealed class GroupNodeTests
    {
        private const string TARGET = "Target";
        private const string RANGED = "Ranged";
        private const float DT = 0.02f;
        private const float EPSILON = 1e-3f;
        private const int MOST_STEPS = 200;

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

        // ---------- Take Attack Slot ----------

        [Test]
        public void TakeAttackSlot_APlaceIsFree_GotOnceTheGroupAnswers_AndAtOnceForTheHolder()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig rig = RigIn(world, new Vector3(2f, 0f, 0f), target);
            var take = new TakeAttackSlotTask { Target = TARGET };

            Assert.AreEqual(TaskStatus.Running, take.Tick(rig.Context, DT), "Asked: the group answers once per step");
            world.BeginStep(DT);
            Assert.AreEqual(TaskStatus.Success, take.Tick(rig.Context, DT), "Got");
            Assert.IsTrue(Holds(world, target, rig));

            Assert.AreEqual(TaskStatus.Success, take.Tick(rig.Context, DT), "Asked again by its holder: Got at once");
        }

        [Test]
        public void TakeAttackSlot_EveryPlaceTaken_BusyAfterWaitTime_AndAsksNoMore()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig late = RigIn(world, new Vector3(0f, 0f, 1.5f), target);       // not nearer than the holder
            var take = new TakeAttackSlotTask { Target = TARGET, WaitTime = 0.3f };
            float waitTime = take.WaitTime.Get(late.Context);
            float waited = 0f;
            TaskStatus answer = TaskStatus.Running;

            while (answer == TaskStatus.Running && waited < waitTime * 3f)
            {
                answer = take.Tick(late.Context, DT);
                waited += DT;
                world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Failure, answer, "Busy");
            Assert.That(waited, Is.EqualTo(waitTime).Within(DT + EPSILON), "After Wait Time");

            // The request went with the answer: a place freed now is not given to it behind its back
            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            world.BeginStep(DT);
            world.BeginStep(DT);
            Assert.IsFalse(Holds(world, target, late), "Busy: it asks again later, itself");
        }

        [Test]
        public void TakeAttackSlot_NoTarget_Busy_OutsideAnyGroup_GotAtOnce()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world, Vector3.zero);

            Assert.AreEqual(TaskStatus.Failure, new TakeAttackSlotTask { Target = TARGET }.Tick(rig.Context, DT),
                "The variable holds nothing");
            Assert.AreEqual(TaskStatus.Failure, new TakeAttackSlotTask().Tick(rig.Context, DT),
                "No Target and no Brain's target");
            Assert.AreEqual(TaskStatus.Failure, new TakeAttackSlotTask().Tick(null, DT));

            var alone = new AgentContext(null, null, 1) { BrainTarget = NewTarget(new Vector3(2f, 0f, 0f), 1) };
            Assert.AreEqual(TaskStatus.Success, new TakeAttackSlotTask().Tick(alone, DT),
                "No group: nothing limits the attack on the Brain's target");
        }

        [Test]
        public void TakeAttackSlot_LeftWhileWaitingForAnAnswer_IsNotGivenThePlaceLater()
        {
            // Known open issue, expected to fail now: a request stays filed after its node is left, and the place freed
            // later goes to an agent that no longer asks (handbook §18.1: a place is asked for every step)
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig waiting = RigIn(world, new Vector3(0f, 0f, 1.5f), target);
            var take = new TakeAttackSlotTask { Target = TARGET, WaitTime = 5f };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(TaskStatus.Running, take.Tick(waiting.Context, DT), "Waits for an answer");
                world.BeginStep(DT);
            }

            take.Interrupt(waiting.Context);                // an arrow led its state away: nobody asks any more
            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            for (int i = 0; i < 3; i++)
                world.BeginStep(DT);

            Assert.IsFalse(Holds(world, target, waiting), "Nobody asks for the place: it is not given");
        }

        [Test]
        public void TakeAttackSlot_ANearerOneAsksWhileTheHolderSwings_TheHolderKeepsThePlaceUntilTheBlowIsOver()
        {
            // Known open issue, expected to fail now: a clearly nearer agent takes the place from the farthest holder
            // even in the middle of its blow, and more strike at once than the target allows (handbook §18.2, rule 3:
            // preemption, but never of a swing that has started)
            const float WINDUP = 0.4f;
            const float RECOVERY = 0.3f;
            const float REACH = 3f;
            const float HOLDER_DISTANCE = REACH - 0.5f;
            const float NEARER_DISTANCE = HOLDER_DISTANCE - AttackSlots.SWAP_MARGIN - 0.5f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            GraphRig striker = RigIn(world, new Vector3(HOLDER_DISTANCE, 0f, 0f), target);
            GraphRig nearer = RigIn(world, new Vector3(0f, 0f, NEARER_DISTANCE), target);
            striker.GiveWeapon(REACH, WINDUP, RECOVERY);
            var strike = new AttackTask { Target = TARGET, Move = "Normal" };
            striker.SetGraph(TakePlaceThen(strike));

            Assert.IsTrue(StepUntil(world, () => strike.IsRunningFor(striker.Context), 1f, striker),
                "Set-up: the striker took the only place and swings");
            Assert.IsTrue(Holds(world, target, striker));

            // All through the swing the nearer one asks: the place stays with the one who swings, the nearer one waits
            var ask = new TakeAttackSlotTask { Target = TARGET, WaitTime = 10f };
            float swinging = 0f;
            while (hits.Hits == 0 && swinging < WINDUP + 1f)
            {
                world.BeginStep(DT);
                Assert.IsTrue(Holds(world, target, striker), $"{swinging:0.00} s into the swing: still the striker's");
                striker.Step(DT);
                Assert.AreEqual(TaskStatus.Running, ask.Tick(nearer.Context, DT), "The nearer one waits");
                swinging += DT;
            }

            Assert.AreEqual(1, hits.Hits, "The blow landed");

            // The blow is over: now the place goes to the nearer one
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < Mathf.CeilToInt(RECOVERY / DT) + 5 && answer == TaskStatus.Running; i++)
            {
                world.BeginStep(DT);
                striker.Step(DT);
                answer = ask.Tick(nearer.Context, DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer, "Got, once the striker's blow is over");
            Assert.IsFalse(Holds(world, target, striker), "The striker's place is the nearer one's now");
        }

        // ---------- Release Attack Slot ----------

        [Test]
        public void ReleaseAttackSlot_GivesThePlaceBack_DoneAtOnce_TheNextWhoAsksGetsIt()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig first = RigIn(world, new Vector3(1f, 0f, 0f), target);
            GraphRig next = RigIn(world, new Vector3(0f, 0f, 3f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, first), "Set-up: the place is taken");
            Assert.AreEqual(TaskStatus.Failure, TakePlace(world, next), "Set-up: Busy");

            Assert.AreEqual(TaskStatus.Success, new ReleaseAttackSlotTask().Tick(first.Context, DT), "Done, at once");
            Assert.IsFalse(Holds(world, target, first));
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, next), "The next who asks gets it");
        }

        [Test]
        public void ReleaseAttackSlot_HoldingNoPlace_OrOutsideAnyGroup_IsDoneAnyway()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world, Vector3.zero, NewTarget(new Vector3(2f, 0f, 0f), 1));

            Assert.AreEqual(TaskStatus.Success, new ReleaseAttackSlotTask().Tick(rig.Context, DT), "Holds none");
            Assert.AreEqual(TaskStatus.Success, new ReleaseAttackSlotTask().Tick(new AgentContext(null, null, 1), DT));
            Assert.AreEqual(TaskStatus.Success, new ReleaseAttackSlotTask().Tick(null, DT));
        }

        [Test]
        public void ReleaseAttackSlot_APlaceOfAGroupOfItsOwn_IsGivenBackToo()
        {
            // Suspected bug, expected to fail now: Release Attack Slot gives back the common places only, so a place
            // Take Attack Slot took in a named group (the archers' Pool "Ranged") is never given back by it
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            target.GetComponent<AttackSlotCapacity>().SetPool(RANGED, 1);
            GraphRig archer = RigIn(world, new Vector3(6f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, archer, RANGED), "Set-up: a place of its group");

            Assert.AreEqual(TaskStatus.Success, new ReleaseAttackSlotTask().Tick(archer.Context, DT));

            Assert.IsFalse(Holds(world, target, archer, RANGED), "The place is back with the target");
        }

        // ---------- Circle Target ----------

        [Test]
        public void CircleTarget_GoesRoundAtRadius_SidewaysAtItsPace_FacingTheTarget()
        {
            const float RADIUS = 3f;
            const float SPEED_SCALE = 0.35f;
            const float SETTLE = 5f;
            const float WATCH = 2f;
            const float KEEPS_TO = 0.1f;
            var rig = new GraphRig(trace: false);
            Transform target = rig.CreateTargetAt(Vector3.zero);
            rig.Agent.Blackboard.Set(TARGET, target);
            rig.Actuator.Position = new Vector3(RADIUS + 1f, 0f, 0f);         // starts off its circle
            var circle = new CircleTargetTask
            {
                Target = TARGET, Radius = RADIUS, SpeedScale = SPEED_SCALE, Sway = 0f,
                ChangeEvery = 100f,                 // one side and one distance all the way: the walk can be measured
                WaitForSlot = false, Duration = 100f
            };
            bool lookedAway = false;
            float worst = 0f;
            float swept = 0f;

            for (int i = 0; i < Mathf.CeilToInt((SETTLE + WATCH) / DT); i++)
            {
                Assert.AreEqual(TaskStatus.Running, circle.Tick(rig.Context, DT));
                lookedAway |= Vector3.Angle(rig.Actuator.Facing, target.position - rig.Actuator.Position) > 1f;
                Vector3 before = rig.Actuator.Position - target.position;
                rig.Actuator.Apply(DT);
                if (i * DT < SETTLE)
                    continue;

                Vector3 after = rig.Actuator.Position - target.position;
                worst = Mathf.Max(worst, Mathf.Abs(after.magnitude - RADIUS));
                swept += Vector3.Angle(before, after);
            }

            float arc = rig.Actuator.Speed * SPEED_SCALE * WATCH / RADIUS * Mathf.Rad2Deg;
            Assert.IsFalse(lookedAway, "Its eyes stay on the target");
            Assert.Less(worst, KEEPS_TO, "Pulled in to its radius and kept there");
            Assert.That(swept, Is.EqualTo(arc).Within(arc * 0.1f), "Goes round sideways, at Speed Scale of its speed");
        }

        [Test]
        public void SquareOff_StepsLeftRightInOut_SquareToTheTarget_WithinAMetreOfItsRadius_NeverStandsStill()
        {
            // The skeleton graph's radius: a fixed line in the world would leave the band within a couple of steps
            const float RADIUS = 5.5f;
            const float BAND = 1f;
            const float WATCH = 120f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f));             // one fights: the other waits
            GraphRig rig = RigIn(world, new Vector3(0f, 0f, -RADIUS), target);
            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS };
            bool lookedAway = false;
            float nearest = float.MaxValue, farthest = 0f;
            var time = new Dictionary<string, float> { ["side"] = 0f, ["forward"] = 0f, ["back"] = 0f };
            float worstOff = 0f;

            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                Assert.AreEqual(TaskStatus.Running, square.Tick(rig.Context, DT));
                lookedAway |= Vector3.Angle(rig.Actuator.Facing, target.position - rig.Actuator.Position) > 1f;
                Vector3 inward = (target.position - rig.Actuator.Position).normalized;
                Vector3 before = rig.Actuator.Position;
                rig.Actuator.Apply(DT);
                rig.Agent.transform.position = rig.Actuator.Position;
                world.BeginStep(DT);
                Vector3 moved = rig.Actuator.Position - before;
                Assert.Greater(moved.sqrMagnitude, 1e-8f, $"Moving at {i * DT:0.00} s: standing still is not its job");
                float distance = (rig.Actuator.Position - target.position).magnitude;
                nearest = Mathf.Min(nearest, distance);
                farthest = Mathf.Max(farthest, distance);

                // Which way relative to the target as it is now: square to it, straight at it or straight away
                float angle = Vector3.Angle(moved, inward);
                string kind = angle < 45f ? "forward" : angle > 135f ? "back" : "side";
                worstOff = Mathf.Max(worstOff, kind == "forward" ? angle : kind == "back" ? 180f - angle : Mathf.Abs(angle - 90f));
                time[kind] += DT;
            }

            float moving = time["side"] + time["forward"] + time["back"];
            Assert.IsFalse(lookedAway, "Its eyes stay on the target");
            Assert.Less(worstOff, 2f, "Every step is left, right, in or out relative to the target — never a diagonal");
            Assert.Greater(time["side"], moving * 0.4f, "Most of its walking is sidesteps");
            Assert.Greater(time["forward"] + time["back"], 0f, "Steps in and out as well");
            Assert.GreaterOrEqual(nearest, RADIUS - BAND - 0.3f, "Not nearer than the band");
            Assert.LessOrEqual(farthest, RADIUS + BAND + 0.3f, "Not farther than the band");
        }

        [Test]
        public void SquareOff_OffItsPlaceOnTheRing_TakesItAtABriskerStep_ThenShufflesAgain()
        {
            const float RADIUS = 5f;
            const float WARY = 0.4f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, RADIUS));           // one attacks from the north
            GraphRig rig = RigIn(world, new Vector3(1f, 0f, RADIUS), target);  // the waiter comes up beside it
            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS, SpeedScale = WARY };

            float away = Fastest(rig, square, 10);          // two on the ring: its place is across the target, far off
            AttackRing ring = world.Roster.EngagementOf(target).Ring;
            rig.Actuator.Position = target.position + AttackSlots.DirectionOf(ring.PlaceOf(rig.Agent).Value) * RADIUS;
            rig.Agent.transform.position = rig.Actuator.Position;
            float home = Fastest(rig, square, 10);

            Assert.That(away, Is.EqualTo(0.6f * rig.Actuator.Speed).Within(0.01f),
                "On its way to its place it goes briskly — but a brisk step, never a run");
            Assert.That(home, Is.EqualTo(WARY * rig.Actuator.Speed).Within(0.01f),
                "Standing on its place it is back to the wary step");
        }

        [Test]
        public void SquareOff_AStepRunsIntoAWall_TurnsAwayAtOnce_InsteadOfStandingAgainstIt()
        {
            // An island with obstacles: a sidestep walked into the edge of the NavMesh and the body stood against it until
            // the step gave up — the animator played stop and stance in the middle of the node. The wall here: nothing
            // east of x = 1 (the body is held at it, as the NavMesh holds an agent at its edge)
            const float RADIUS = 4f;
            const float WARY = 0.4f;
            const float WALL = 1f;
            const float WATCH = 60f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f));             // one fights from the north
            GraphRig rig = RigIn(world, new Vector3(0f, 0f, -RADIUS), target); // the waiter, south, near the wall
            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS, SpeedScale = WARY };
            float pace = WARY * rig.Actuator.Speed;
            float stuck = 0f, longestStuck = 0f;
            int heldByTheWall = 0;

            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                square.Tick(rig.Context, DT);
                Vector3 before = rig.Actuator.Position;
                rig.Actuator.Apply(DT);
                Vector3 at = rig.Actuator.Position;
                if (at.x > WALL)
                {
                    at.x = WALL;
                    heldByTheWall++;
                }

                rig.Actuator.Position = at;
                rig.Agent.transform.position = at;
                world.BeginStep(DT);

                float rate = (at - before).magnitude / DT;
                stuck = rate < pace * 0.5f ? stuck + DT : 0f;
                longestStuck = Mathf.Max(longestStuck, stuck);
            }

            Assert.Greater(heldByTheWall, 0, "Set-up: its steps do reach the wall");
            Assert.Less(longestStuck, 0.5f, "Against the wall it does not stand: the step turns away");
        }

        [Test]
        public void SquareOff_ThreeOnTheRing_StepsAreNotCutShort_NeverHurriesBackToItsPlace()
        {
            // Its steps used to be cut short the moment they were 12° off its place — half a second at this radius — and
            // a hurried step back came straight after: a new direction every half a second
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
            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS, SpeedScale = WARY };
            var last = new int[rigs.Count];
            var stretch = new float[rigs.Count];
            var walks = new List<float>();
            int hurried = 0;

            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                for (int k = 0; k < rigs.Count; k++)
                {
                    GraphRig rig = rigs[k];
                    square.Tick(rig.Context, DT);
                    Vector3 velocity = rig.Actuator.Body.Velocity;
                    int kind = StepKind(velocity, target.position - rig.Actuator.Position);
                    bool counting = i * DT > SETTLE;
                    if (counting && velocity.magnitude > (WARY + 0.05f) * rig.Actuator.Speed)
                        hurried++;
                    if (kind != last[k])
                    {
                        if (counting && last[k] != 0)
                            walks.Add(stretch[k]);
                        stretch[k] = 0f;
                    }

                    stretch[k] += DT;
                    last[k] = kind;
                    rig.Actuator.Apply(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                world.BeginStep(DT);
            }

            Assert.Greater(walks.Count, 10, "It does take steps");
            Assert.AreEqual(0, hurried, "Nobody moved its place: it never hurries back to it");
            Assert.Greater(walks.Average(), 0.8f, "A step is not cut short half a second in");
        }


        // Which way the body was told to go, relative to the target: 0 — stands, 1 / 2 — left / right, 3 / 4 — in / out
        private static int StepKind(Vector3 velocity, Vector3 toTarget)
        {
            if (velocity.magnitude < 0.05f)
                return 0;
            Vector3 inward = toTarget.normalized;
            float along = Vector3.Dot(velocity.normalized, inward);
            return along > 0.7f ? 3 : along < -0.7f ? 4 : Vector3.Cross(inward, velocity).y > 0f ? 1 : 2;
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

        [Test]
        public void SquareOff_NineWaiting_SpreadRoundTheTarget_InsteadOfBunchingOnTheSideTheyCameFrom()
        {
            const float RADIUS = 5f;
            const float WATCH = 30f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(1f, 0f, 0f));               // one attacks; the rest wait
            var waiting = new List<GraphRig>();
            for (int i = 0; i < 9; i++)                                        // all of them come from the south-west
                waiting.Add(RigIn(world, new Vector3(-4f - i * 0.4f, 0f, -4f), target));

            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS };
            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                foreach (GraphRig rig in waiting)
                {
                    square.Tick(rig.Context, DT);
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
            {
                Assert.Greater(angles[i] - angles[i - 1], 15f,
                    "Two of them stand on top of each other: the places are not spread");
            }

            Assert.Greater(angles[angles.Count - 1] - angles[0], 200f,
                "They are round the target, not all on the side they came from");
        }

        [Test]
        public void SquareOff_GotSlotTheMomentAPlaceIsFree()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig waiting = RigIn(world, new Vector3(-4f, 0f, 0f), target);
            var square = new SquareOffTask { Target = TARGET };

            for (int i = 0; i < Mathf.CeilToInt(2f / DT); i++)
            {
                Assert.AreEqual(TaskStatus.Running, square.Tick(waiting.Context, DT), "Every place is taken: it waits");
                world.BeginStep(DT);
            }

            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            world.BeginStep(DT);

            Assert.AreEqual(TaskStatus.Success, square.Tick(waiting.Context, DT), "Got Slot");
            Assert.IsTrue(Holds(world, target, waiting));
        }

        [Test]
        public void SquareOff_NoTarget_Lost()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);

            Assert.AreEqual(TaskStatus.Failure, new SquareOffTask { Target = TARGET }.Tick(rig.Context, DT));
        }

        [Test]
        public void CircleTarget_WaitForSlot_AsksAllTheWhile_GotSlotTheMomentAPlaceIsFree()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig waiting = RigIn(world, new Vector3(-4f, 0f, 0f), target);
            var circle = new CircleTargetTask { Target = TARGET };

            for (int i = 0; i < Mathf.CeilToInt(2f / DT); i++)
            {
                Assert.AreEqual(TaskStatus.Running, circle.Tick(waiting.Context, DT), "Every place is taken: circles");
                world.BeginStep(DT);
            }

            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            world.BeginStep(DT);

            Assert.AreEqual(TaskStatus.Success, circle.Tick(waiting.Context, DT),
                "Got Slot: the freed place went to the one waiting");
            Assert.IsTrue(Holds(world, target, waiting));
        }

        [Test]
        public void CircleTarget_SeveralWait_TheNearestGetsTheFreedPlace()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig near = RigIn(world, new Vector3(0f, 0f, 3f), target);
            GraphRig far = RigIn(world, new Vector3(0f, 0f, -6f), target);
            var circle = new CircleTargetTask { Target = TARGET };             // one graph on both: one node

            for (int i = 0; i < 5; i++)
            {
                circle.Tick(near.Context, DT);
                circle.Tick(far.Context, DT);
                world.BeginStep(DT);
            }

            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            world.BeginStep(DT);

            Assert.AreEqual(TaskStatus.Success, circle.Tick(near.Context, DT), "The nearer one: Got Slot");
            Assert.AreEqual(TaskStatus.Running, circle.Tick(far.Context, DT), "The farther one goes on circling");
        }

        [Test]
        public void CircleTarget_WithoutWaitForSlot_EndsAfterDuration_AsksForNoPlace()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig rig = RigIn(world, new Vector3(4f, 0f, 0f), target);
            var circle = new CircleTargetTask { Target = TARGET, WaitForSlot = false, Duration = 1.5f };
            float duration = circle.Duration.Get(rig.Context);
            float circled = 0f;
            TaskStatus answer = TaskStatus.Running;

            while (answer == TaskStatus.Running && circled < duration * 2f)
            {
                answer = circle.Tick(rig.Context, DT);
                circled += DT;
                world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer);
            Assert.That(circled, Is.EqualTo(duration).Within(DT + EPSILON), "Done after Duration");
            CollectionAssert.IsEmpty(world.Roster.EngagementOf(target).Tokens.Pools, "It never asked for a place");

            var once = new CircleTargetTask { Target = TARGET, WaitForSlot = false, Duration = 0f };
            Assert.AreEqual(TaskStatus.Success, once.Tick(rig.Context, DT), "Duration 0: done at once");
        }

        [Test]
        public void CircleTarget_NoTarget_Lost_OutsideAnyGroup_GotSlotAtOnce()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world, Vector3.zero);

            Assert.AreEqual(TaskStatus.Failure, new CircleTargetTask { Target = TARGET }.Tick(rig.Context, DT),
                "Lost: the variable holds nothing");
            Assert.AreEqual(TaskStatus.Failure, new CircleTargetTask().Tick(rig.Context, DT),
                "Lost: no Target and no Brain's target");
            Assert.AreEqual(TaskStatus.Failure, new CircleTargetTask().Tick(null, DT));

            var alone = new AgentContext(null, null, 1)
            {
                Actuator = new TestActuator(), BrainTarget = NewTarget(new Vector3(4f, 0f, 0f), 1)
            };
            Assert.AreEqual(TaskStatus.Success, new CircleTargetTask().Tick(alone, DT),
                "Got Slot: no group, nothing limits the attack");
        }

        [Test]
        public void CircleTarget_LeftWhileWaiting_IsNotGivenThePlaceLater()
        {
            // Known open issue, expected to fail now: the request Circle Target filed outlives the node, and the place
            // freed later goes to an agent that no longer waits for it (handbook §18.1: a place is asked every step)
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(1f, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig waiting = RigIn(world, new Vector3(-4f, 0f, 0f), target);
            var circle = new CircleTargetTask { Target = TARGET };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(TaskStatus.Running, circle.Tick(waiting.Context, DT), "Circles and waits");
                world.BeginStep(DT);
            }

            circle.Interrupt(waiting.Context);              // an arrow led its state away: it waits no more
            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            for (int i = 0; i < 3; i++)
                world.BeginStep(DT);

            Assert.IsFalse(Holds(world, target, waiting), "Nobody asks for the place: it is not given");
        }

        // ---------- a nearer one takes a place ----------

        [Test]
        public void TakeAttackSlot_AClearlyNearerOneAsks_TheFarthestHolderLosesItsPlace()
        {
            const float FAR = 5f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig near = RigIn(world, new Vector3(2f, 0f, 0f), target);
            GraphRig far = RigIn(world, new Vector3(-FAR, 0f, 0f), target);
            TaskStatus[] holders = AskTogether(world, new TakeAttackSlotTask { Target = TARGET }, near, far);
            CollectionAssert.AreEqual(new[] { TaskStatus.Success, TaskStatus.Success }, holders, "Set-up: both hold");

            // Nearer than the farthest holder by more than the margin
            GraphRig newcomer = RigIn(world, new Vector3(0f, 0f, FAR - AttackSlots.SWAP_MARGIN - 0.5f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, newcomer), "Got: the farthest holder's place");

            Assert.IsFalse(Holds(world, target, far), "The farthest holder has lost its place");
            Assert.IsTrue(Holds(world, target, near), "The nearer holder keeps its place");
            Assert.IsTrue(Holds(world, target, newcomer));
        }

        [Test]
        public void TakeAttackSlot_TheOneWhoAsksIsNearerByLessThanTheMargin_TheHolderKeepsIt()
        {
            const float HOLDER_DISTANCE = 3f;
            const float ASKING_DISTANCE = HOLDER_DISTANCE - AttackSlots.SWAP_MARGIN + 0.25f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 1);
            GraphRig holder = RigIn(world, new Vector3(HOLDER_DISTANCE, 0f, 0f), target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
            GraphRig nearer = RigIn(world, new Vector3(0f, 0f, ASKING_DISTANCE), target);

            Assert.AreEqual(TaskStatus.Failure, TakePlace(world, nearer),
                "Busy: two at about one distance do not swap every step");
            Assert.IsTrue(Holds(world, target, holder), "The holder keeps its place");
        }

        // ---------- the target's places (Attack Slots) ----------

        [Test]
        public void AttackSlotCapacity_TwoPlaces_TwoOfThreeGetOne_TheThirdIsBusy()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig farthest = RigIn(world, new Vector3(-2.5f, 0f, 0f), target);
            GraphRig[] rigs =
            {
                RigIn(world, new Vector3(1f, 0f, 0f), target), RigIn(world, new Vector3(0f, 0f, 2f), target), farthest
            };

            TaskStatus[] answers = AskTogether(world, new TakeAttackSlotTask { Target = TARGET }, rigs);

            Assert.AreEqual(target.GetComponent<AttackSlotCapacity>().Slots, CountOf(answers, TaskStatus.Success),
                "As many attack at once as the target allows");
            Assert.AreEqual(TaskStatus.Failure, answers[Array.IndexOf(rigs, farthest)],
                "The farthest finds every place taken: Busy");
        }

        [Test]
        public void AttackSlotCapacity_AGroupOfItsOwn_HasItsOwnNumber_ApartFromTheCommonPlaces()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            var capacity = target.GetComponent<AttackSlotCapacity>();
            capacity.SetPool(RANGED, 1);

            Assert.AreEqual(2, capacity.CapacityOf(AttackSlots.POOL));
            Assert.AreEqual(1, capacity.CapacityOf(RANGED));
            Assert.AreEqual(capacity.Slots, capacity.CapacityOf("Thrown"), "A group with no number of its own");

            GraphRig[] archers =
            {
                RigIn(world, new Vector3(6f, 0f, 0f), target), RigIn(world, new Vector3(0f, 0f, 6.5f), target)
            };
            GraphRig[] melee =
            {
                RigIn(world, new Vector3(1f, 0f, 0f), target), RigIn(world, new Vector3(0f, 0f, -1.5f), target)
            };
            TaskStatus[] shots = AskTogether(world, new TakeAttackSlotTask { Target = TARGET, Pool = RANGED }, archers);
            TaskStatus[] blows = AskTogether(world, new TakeAttackSlotTask { Target = TARGET }, melee);

            Assert.AreEqual(capacity.CapacityOf(RANGED), CountOf(shots, TaskStatus.Success),
                "One archer shoots, the other waits for a place of its own group");
            Assert.AreEqual(capacity.CapacityOf(AttackSlots.POOL), CountOf(blows, TaskStatus.Success),
                "The common places are not taken by the archers");

            GraphRig shooter = archers[Array.IndexOf(shots, TaskStatus.Success)];
            Assert.IsTrue(Holds(world, target, shooter, RANGED), "The archer holds a place of its own group");
            Assert.IsFalse(Holds(world, target, shooter), "…not a common place");
        }

        [Test]
        public void AttackSlotCapacity_NoPlaces_NobodyAttacks_ANegativeNumberIsNone()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 0);
            var capacity = target.GetComponent<AttackSlotCapacity>();
            capacity.Slots = -3;
            capacity.SetPool(RANGED, -1);

            Assert.AreEqual(0, capacity.Slots);
            Assert.AreEqual(0, capacity.CapacityOf(RANGED));
            Assert.AreEqual(TaskStatus.Failure, TakePlace(world, RigIn(world, new Vector3(1f, 0f, 0f), target)),
                "Busy: the target lets nobody attack it");
        }

        [Test]
        public void AttackSlotCapacity_NoneOnTheTarget_OnePlacePerThreeAgentsOfTheGroup()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero);                       // no Attack Slots on it
            var rigs = new GraphRig[4];
            for (int i = 0; i < rigs.Length; i++)
                rigs[i] = RigIn(world, new Vector3(1f + i, 0f, 0f), target);

            TaskStatus[] answers = AskTogether(world, new TakeAttackSlotTask { Target = TARGET }, rigs);

            Assert.AreEqual(RoleAssignment.AttackersFor(rigs.Length), CountOf(answers, TaskStatus.Success));
        }

        // ---------- helpers ----------

        /// <summary>
        /// An awake agent of the world (the places keep the requests of awake agents only), thinking every step; its
        /// body and its object stand together, as on a character (the places measure the object). The target goes
        /// into its Target variable.
        /// </summary>
        private static GraphRig RigIn(IAIWorld world, Vector3 at, Transform target = null)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            if (target != null)
                rig.Agent.Blackboard.Set(TARGET, target);
            return rig;
        }

        /// <summary>The object to fight; places — how many its Attack Slots give (negative — it has none).</summary>
        private Transform NewTarget(Vector3 at, int places = -1)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            if (places >= 0)
                go.AddComponent<AttackSlotCapacity>().Slots = places;
            return go.transform;
        }

        /// <summary>One agent at that place takes the target's attack place, so the rest have to wait.</summary>
        private static void GivePlaceTo(IAIWorld world, Transform target, Vector3 at)
        {
            GraphRig holder = RigIn(world, at, target);
            Assert.AreEqual(TaskStatus.Success, TakePlace(world, holder), "Set-up: the place is taken");
        }

        private static bool Holds(IAIWorld world, Transform target, GraphRig rig, string pool = AttackSlots.POOL) =>
            world.Roster.EngagementOf(target).Tokens.Holds(rig.Agent, pool);

        private static int CountOf(IEnumerable<TaskStatus> answers, TaskStatus status) =>
            answers.Count(answer => answer == status);

        /// <summary>The agent asks with Take Attack Slot until it answers.</summary>
        private static TaskStatus TakePlace(IAIWorld world, GraphRig rig, string pool = "") =>
            AskTogether(world, new TakeAttackSlotTask { Target = TARGET, Pool = pool }, rig)[0];

        /// <summary>
        /// Every agent asks with the same node (one graph on all of them), step after step, until each has answered;
        /// the group answers between the steps.
        /// </summary>
        private static TaskStatus[] AskTogether(IAIWorld world, AgentTask ask, params GraphRig[] rigs)
        {
            var answers = new TaskStatus[rigs.Length];
            for (int step = 0; step < MOST_STEPS; step++)
            {
                bool waiting = false;
                for (int i = 0; i < rigs.Length; i++)
                {
                    if (answers[i] != TaskStatus.Running)
                        continue;

                    answers[i] = ask.Tick(rigs[i].Context, DT);
                    waiting |= answers[i] == TaskStatus.Running;
                }

                if (!waiting)
                    break;

                world.BeginStep(DT);
            }

            return answers;
        }

        /// <summary>The world's step, then the agents' (as in the game), until the condition holds.</summary>
        private static bool StepUntil(IAIWorld world, Func<bool> condition, float seconds, params GraphRig[] rigs)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                if (condition())
                    return true;

                world.BeginStep(DT);
                foreach (GraphRig rig in rigs)
                    rig.Step(DT);
            }

            return condition();
        }

        // Brain → Daily Routine → Do → Take Attack Slot —Got→ the action (a repeating blow runs blow after blow)
        private static BehaviorGraph TakePlaceThen(AgentTask action)
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            AgentState take = g.Block(new TakeAttackSlotTask { Target = TARGET });
            g.Do(g.Always(), take);
            g.Then(take, g.Block(action, "Action"));
            return g.Build();
        }
    }
}
