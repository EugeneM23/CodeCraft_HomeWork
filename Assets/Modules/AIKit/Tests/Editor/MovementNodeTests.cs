using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The movement blocks on a real agent with a fake body: Chase runs to its object and stands at Stop Distance,
    /// Flank comes up from its own side of the target (opposite the one who attacks first, a share of the circle per
    /// place, straight when there is nobody to take a side from), Kite keeps its shooting band, Move To walks to a
    /// point or an object. A block is ticked the way its node ticks it, and the body moves as it was told between the
    /// ticks.
    /// </summary>
    public sealed class MovementNodeTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;
        private const float EPSILON = 1e-3f;

        /// <summary>How near to its place on the circle a flanker counts as standing there (ApproachTask).</summary>
        private const float ARRIVE_DISTANCE = 0.5f;

        private readonly List<Object> _created = new();

        /// <summary>A navigator that finds no way anywhere (the target is on another island).</summary>
        private sealed class NoWayNavigator : INavigator
        {
            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Mathf.Infinity;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                Status = NavStatus.Unreachable;
                return NavVerdict.Unreachable;
            }

            public void Clear() => Status = NavStatus.Idle;

            public void Step(float dt)
            {
            }
        }

        /// <summary>A line of sight the test opens and closes (a wall between the agent and the target).</summary>
        private sealed class SwitchableSight : ISightProbe
        {
            public bool Clear;

            public bool HasLineOfSight(Vector3 from, Vector3 to) => Clear;
        }

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

        // ---------- Chase ----------

        [Test]
        public void Chase_TargetAway_RunsToIt_StandsAtStopDistance_AndRunsAgainWhenItMoves()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(6f, 0f, 0f));
            var chase = new ChaseTask { Target = TARGET, StopDistance = 1.5f };
            float stop = chase.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Running, Run(rig, chase, 3f), "Without Finish When Caught it stays by it");
            Assert.That(FlatDistance(rig.Actuator.Position, target.position), AtStopDistance(rig, stop),
                "Stands at Stop Distance: not short of it, not on the target");

            Vector3 standing = rig.Actuator.Position;
            Run(rig, chase, 0.5f);
            Assert.AreEqual(standing, rig.Actuator.Position, "Close enough: it stands");

            target.position = new Vector3(6f, 0f, 5f);
            Assert.AreEqual(TaskStatus.Running, Run(rig, chase, 3f));
            Assert.That(FlatDistance(rig.Actuator.Position, target.position), AtStopDistance(rig, stop),
                "The target stepped away: it runs after it again");
        }

        [Test]
        public void Chase_FinishWhenCaught_EndsCaught_AtStopDistance()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(0f, 0f, 5f));
            var chase = new ChaseTask { Target = TARGET, StopDistance = 2f, FinishWhenCaught = true };
            float stop = chase.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, chase, 3f), "Caught");
            Assert.That(FlatDistance(rig.Actuator.Position, target.position), AtStopDistance(rig, stop));
            Assert.AreEqual(NavStatus.Idle, rig.Context.Navigation.Status, "The way is dropped");
        }

        [Test]
        public void Chase_NoTargetOfItsOwn_RunsToTheBrainsTarget()
        {
            GraphRig rig = NewRig();
            Transform brainsTarget = NewObject(new Vector3(-4f, 0f, 3f));
            rig.Context.BrainTarget = brainsTarget;
            var chase = new ChaseTask { StopDistance = 1f, FinishWhenCaught = true };
            float stop = chase.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, chase, 3f));
            Assert.That(FlatDistance(rig.Actuator.Position, brainsTarget.position), AtStopDistance(rig, stop));
        }

        [Test]
        public void Chase_StopDistanceIsOverTheGround_ATargetHighAboveIsCaughtFromBelow()
        {
            GraphRig rig = NewRig();
            BindObject(rig, new Vector3(1f, 3f, 0f));          // 1 m away over the ground, 3 m up (a ledge, a flyer)
            var chase = new ChaseTask { Target = TARGET, StopDistance = 1.5f, FinishWhenCaught = true };

            Assert.AreEqual(TaskStatus.Success, chase.Tick(rig.Context, DT), "Within Stop Distance over the ground");
        }

        [Test]
        public void ChaseAndFlank_NoTarget_OrItIsGone_Lost()
        {
            GraphRig rig = NewRig();

            Assert.AreEqual(TaskStatus.Failure, new ChaseTask { Target = TARGET }.Tick(rig.Context, DT),
                "The variable holds nothing");
            Assert.AreEqual(TaskStatus.Failure, new FlankTask { Target = TARGET }.Tick(rig.Context, DT),
                "The variable holds nothing");
            Assert.AreEqual(TaskStatus.Failure, new ChaseTask().Tick(rig.Context, DT), "No Target, no Brain's target");
            Assert.AreEqual(TaskStatus.Failure, new FlankTask().Tick(rig.Context, DT), "No Target, no Brain's target");
            Assert.AreEqual(TaskStatus.Failure, new ChaseTask().Tick(null, DT));

            Transform target = BindObject(rig, new Vector3(5f, 0f, 0f));
            var chase = new ChaseTask { Target = TARGET };
            Assert.AreEqual(TaskStatus.Running, chase.Tick(rig.Context, DT));
            Object.DestroyImmediate(target.gameObject);

            Assert.AreEqual(TaskStatus.Failure, chase.Tick(rig.Context, DT), "The object is gone in the middle");
            Assert.AreEqual(NavStatus.Idle, rig.Context.Navigation.Status, "The way is dropped");
        }

        [Test]
        public void ChaseAndFlank_NoWayToTheTarget_Lost()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig leader = RigIn(world, new Vector3(-2f, 0f, 0f));
            GivePlace(world, leader, target);
            GraphRig rig = RigIn(world, new Vector3(-5f, 0f, 1f), new NoWayNavigator());
            rig.Agent.Blackboard.Set(TARGET, target);

            Assert.AreEqual(TaskStatus.Failure, new ChaseTask { Target = TARGET }.Tick(rig.Context, DT));
            Assert.AreEqual(TaskStatus.Failure, new FlankTask { Target = TARGET }.Tick(rig.Context, DT),
                "No way to its side, nor straight to the target");
        }

        [Test]
        public void Chase_TheTargetStirsOnTheEdgeOfStopDistance_TheBodyStandsStill_AndFollowsWhenItReallyGoes()
        {
            const float STOP = 2f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig rig = RigIn(world, new Vector3(-5f, 0f, 0f));
            rig.Agent.Blackboard.Set(TARGET, target);
            var chase = new ChaseTask { Target = TARGET, StopDistance = STOP };
            Run(rig, chase, 4f);                                     // comes up and stands at Stop Distance
            Vector3 stood = rig.Actuator.Position;
            float rest = FlatDistance(stood, target.position);
            float stir = STOP - rest + 0.2f;                         // a stir that puts the gap 0.2 m past Stop Distance

            // the target stirs on the spot, over the edge of Stop Distance and back
            for (int i = 0; i < 20; i++)
            {
                target.position = new Vector3(i % 2 == 0 ? stir : 0f, 0f, 0f);
                chase.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            Assert.LessOrEqual(Vector3.Distance(rig.Actuator.Position, stood), 0.1f,
                "It does not take a path and drop it every other step — no shivering on the edge");

            target.position = new Vector3(2f, 0f, 0f);               // and now it really goes
            Run(rig, chase, 2f);
            Assert.LessOrEqual(FlatDistance(rig.Actuator.Position, target.position), STOP + EPSILON,
                "A target that really walks away is followed");
        }

        // ---------- Flank ----------

        [Test]
        public void Flank_SomebodyElseHoldsAPlace_ComesUpToAPlaceOfItsOwn_InPlaceAtStopDistance()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig leader = RigIn(world, new Vector3(-2f, 0f, 0f));
            GivePlace(world, leader, target);
            GraphRig flanker = RigIn(world, new Vector3(-4f, 0f, 3f));          // starts on the leader's side
            flanker.Agent.Blackboard.Set(TARGET, target);
            var flank = new FlankTask { Target = TARGET, StopDistance = 1.5f, BypassRadius = 1.5f };
            float stop = flank.StopDistance.Get(flanker.Context);

            Assert.AreEqual(TaskStatus.Success, Run(flanker, flank, 5f), "In Place");

            Vector3 mine = flanker.Actuator.Position - target.position;
            Assert.That(Flat(mine).magnitude, Is.EqualTo(stop).Within(ARRIVE_DISTANCE + EPSILON), "At Stop Distance");
            float tolerance = Mathf.Asin(Mathf.Min(1f, ARRIVE_DISTANCE / stop)) * Mathf.Rad2Deg;
            Assert.That(Vector3.Angle(Flat(mine), Flat(leader.Actuator.Position - target.position)),
                Is.EqualTo(180f).Within(tolerance), "Two of them: the target is between the two");
        }

        [Test]
        public void Flank_TenAttackers_EachGetsAPlaceOfItsOwn_SpreadEvenlyRoundTheTarget()
        {
            const float STOP = 4f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 10);
            GraphRig first = RigIn(world, new Vector3(-2f, 0f, 0f));
            GivePlace(world, first, target);                                     // somebody attacks: the rest flank
            var flank = new FlankTask { Target = TARGET, StopDistance = STOP, BypassRadius = STOP };
            var flankers = new List<GraphRig>();

            for (int i = 0; i < 9; i++)
            {
                // they all come from the same side of the target — the ring must still spread them round it
                GraphRig flanker = RigIn(world, new Vector3(-7f - 0.5f * i, 0f, 1f + 0.4f * i));
                flanker.Agent.Blackboard.Set(TARGET, target);
                flankers.Add(flanker);
            }

            RunTogether(world, flankers, flank, 14f);

            // the ring is cut into ten: the one in the fight and the nine round it, 36° apart
            var bearings = flankers.Select(flanker => Bearing(flanker.Actuator.Position - target.position)).ToList();
            bearings.Add(Bearing(first.Actuator.Position - target.position));
            float gap = 360f / bearings.Count;
            float tolerance = Mathf.Asin(Mathf.Min(1f, ARRIVE_DISTANCE / STOP)) * Mathf.Rad2Deg;

            bearings.Sort();
            for (int i = 0; i < bearings.Count; i++)
            {
                float next = i + 1 < bearings.Count ? bearings[i + 1] : bearings[0] + 360f;
                Assert.That(next - bearings[i], Is.EqualTo(gap).Within(2 * tolerance + 1f),
                    "Evenly, all the way round: no two on one place and no empty side");
            }
        }

        [Test]
        public void Flank_WideBypassRadius_GoesRoundTheTarget_WhereNoBypassGoesPastIt()
        {
            const float STOP = 1.5f;
            var start = new Vector3(-4f, 0f, 0.5f);           // it comes from the west

            // A fight of its own for each way round, so the two flankers do not take each other's place. The agent in
            // the fight anchors the ring on the west, the flanker comes from the west too — and two on a ring stand
            // opposite, so its place is across the target and it has to get there somehow
            float Closest(float bypassRadius, out TaskStatus answer)
            {
                var world = new AIWorld();
                Transform target = NewTarget(Vector3.zero, 2);
                GivePlace(world, RigIn(world, new Vector3(-2f, 0f, 0f)), target);
                return ClosestOnTheNearHalf(world, target, start, STOP, bypassRadius, out answer);
            }

            float wide = Closest(3f, out TaskStatus wideAnswer);
            float straight = Closest(STOP, out TaskStatus straightAnswer);       // not above Stop Distance: no way round

            Assert.AreEqual(TaskStatus.Success, wideAnswer, "In Place, the long way round");
            Assert.AreEqual(TaskStatus.Success, straightAnswer, "In Place, the short way");
            Assert.Greater(wide, STOP, "A wide way round keeps off the target until it is on its side");
            Assert.Less(straight, STOP - ARRIVE_DISTANCE, "Bypass Radius not above Stop Distance: straight to its place, past the target");
        }

        [Test]
        public void Flank_ItsPlaceIsTheOneNearestToWhereItStands_AndItKeepsIt()
        {
            const float STOP = 3f;
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 3);
            // three share the circle: the one in the fight anchors it at the west, so the places are west, 30° and 150°
            GivePlace(world, RigIn(world, new Vector3(-2.5f, 0f, 0f)), target);
            GivePlace(world, RigIn(world, new Vector3(0f, 0f, 2.5f)), target);   // the second holds the one at 30°
            GraphRig third = RigIn(world, new Vector3(0f, 0f, -5f));             // it comes from the south
            GivePlace(world, third, target);
            var flank = new FlankTask { Target = TARGET, StopDistance = STOP, BypassRadius = STOP };

            Assert.AreEqual(TaskStatus.Success, Run(third, flank, 5f), "In Place");

            Vector3 mine = Flat(third.Actuator.Position - target.position);
            Assert.That(mine.magnitude, Is.EqualTo(STOP).Within(ARRIVE_DISTANCE + EPSILON));
            Assert.Less(Vector3.Angle(mine, Vector3.back), 60f,
                "The nearest place to where it came from (150°), not the far one: it does not walk round for nothing");

            Vector3 stood = third.Actuator.Position;
            Assert.AreEqual(TaskStatus.Success, Run(third, flank, 2f), "In Place again");
            Assert.LessOrEqual(Vector3.Distance(third.Actuator.Position, stood), ARRIVE_DISTANCE + EPSILON,
                "Its place does not move under it while it keeps coming");
        }

        [Test]
        public void Flank_NobodyToTakeASideFrom_GoesStraight_InPlaceAtStopDistance()
        {
            var world = new AIWorld();
            Transform target = NewTarget(Vector3.zero, 2);
            GraphRig flanker = RigIn(world, new Vector3(-5f, 0f, 1f));
            flanker.Agent.Blackboard.Set(TARGET, target);
            var flank = new FlankTask { Target = TARGET, StopDistance = 1.5f, BypassRadius = 3f };

            AssertGoesStraight(flanker, flank, target, "Alone (nobody attacks)");

            // It holds the first place itself and another holds the second: the first goes straight in
            PutAt(flanker, new Vector3(-5f, 0f, -1f));
            GivePlace(world, flanker, target);
            GivePlace(world, RigIn(world, new Vector3(5f, 0f, 0f)), target);
            AssertGoesStraight(flanker, flank, target, "The first attacker");
        }

        // ---------- Kite ----------

        [Test]
        public void Kite_TooNear_BacksAwayFacingTheTarget_ToTheMiddleOfTheBand_ThenInRange()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(2f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float middle = (kite.MinDistance.Get(rig.Context) + kite.MaxDistance.Get(rig.Context)) * 0.5f;
            bool lookedAway = false;

            TaskStatus answer = Run(rig, kite, 5f,
                () => lookedAway |= Vector3.Angle(rig.Actuator.Facing, target.position - rig.Actuator.Position) > 1f);

            Assert.AreEqual(TaskStatus.Success, answer, "In Range");
            Assert.IsFalse(lookedAway, "Backs away with its eyes on the target");
            Assert.That(FlatDistance(rig.Actuator.Position, target.position),
                Is.InRange(middle - EPSILON, middle + Stride(rig) + EPSILON),
                "Backed away to the middle of the band, not just past its near edge");
        }

        [Test]
        public void Kite_TooFar_ComesCloser_InRangeAtMaxDistance()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(0f, 0f, 12f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float max = kite.MaxDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, kite, 3f), "In Range");
            Assert.That(FlatDistance(rig.Actuator.Position, target.position), AtStopDistance(rig, max));
        }

        [Test]
        public void Kite_InsideTheBand_InRangeAtOnce_StandingAndFacingTheTarget()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(0f, 0f, -7f));       // inside the default band
            int stops = rig.Actuator.Stops;

            Assert.AreEqual(TaskStatus.Success, new KiteTask { Target = TARGET }.Tick(rig.Context, DT));
            Assert.Greater(rig.Actuator.Stops, stops, "Stands to shoot");
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, target.position - rig.Actuator.Position), 1f,
                "Faces the target");
        }

        [Test]
        public void Kite_CorneredTooNear_ShootsAnywayAfterRetreatTime()
        {
            GraphRig rig = NewRig();
            BindObject(rig, new Vector3(2f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, RetreatTime = 1.5f };
            float retreatTime = kite.RetreatTime.Get(rig.Context);
            float backingAway = 0f;
            TaskStatus answer = TaskStatus.Running;

            // A wall at its back: the body does not get away (it is never moved), the target stays too near
            while (answer == TaskStatus.Running && backingAway < retreatTime * 2f)
            {
                answer = kite.Tick(rig.Context, DT);
                backingAway += DT;
            }

            Assert.AreEqual(TaskStatus.Success, answer, "In Range: better shoot than run forever");
            Assert.That(backingAway, Is.EqualTo(retreatTime).Within(DT + EPSILON));
        }

        [Test]
        public void Kite_NeedsSight_AWallInTheWay_NotInRange_UntilTheLineOpens()
        {
            const float SECONDS = 0.5f;
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(7f, 0f, 0f));
            var sight = new SwitchableSight { Clear = false };
            rig.Context.Sight = sight;
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            Vector3 start = rig.Actuator.Position;

            Assert.AreEqual(TaskStatus.Running, Run(rig, kite, SECONDS), "In the band, but no shot from here");
            Assert.Greater(Vector3.Distance(start, rig.Actuator.Position), rig.Actuator.Speed * SECONDS * 0.5f,
                "It steps aside to find a clear line");
            Assert.That(FlatDistance(rig.Actuator.Position, target.position),
                Is.InRange(kite.MinDistance.Get(rig.Context), kite.MaxDistance.Get(rig.Context)),
                "…keeping its band");

            sight.Clear = true;
            Assert.AreEqual(TaskStatus.Success, kite.Tick(rig.Context, DT), "The line is open: In Range");

            sight.Clear = false;
            var blind = new KiteTask { Target = TARGET, NeedsSight = false };
            Assert.AreEqual(TaskStatus.Success, blind.Tick(rig.Context, DT), "Needs Sight off: a wall does not matter");
        }

        [Test]
        public void Kite_NoTarget_Lost()
        {
            GraphRig rig = NewRig();

            Assert.AreEqual(TaskStatus.Failure, new KiteTask { Target = TARGET }.Tick(rig.Context, DT),
                "The variable holds nothing");
            Assert.AreEqual(TaskStatus.Failure, new KiteTask().Tick(rig.Context, DT), "No Target, no Brain's target");
            Assert.AreEqual(TaskStatus.Failure, new KiteTask().Tick(null, DT));
        }

        // ---------- Move To ----------

        [Test]
        public void MoveTo_APoint_WalksThere_ReachPositionWithinStopDistance()
        {
            GraphRig rig = NewRig();
            var point = new Vector3(5f, 0f, -2f);
            var move = new MoveToTask(point, stopDistance: 0.3f);
            float stop = move.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, move, 3f), "Reach Position");
            Assert.LessOrEqual(Vector3.Distance(rig.Actuator.Position, point), stop + EPSILON);
            Assert.AreEqual(NavStatus.Idle, rig.Context.Navigation.Status, "Arrived: the way is dropped");
        }

        [Test]
        public void MoveTo_AnObject_FollowsItWhileItMoves()
        {
            GraphRig rig = NewRig();
            Transform target = BindObject(rig, new Vector3(5f, 0f, 0f));
            var move = new MoveToTask { Target = TARGET, StopDistance = 0.3f };
            float stop = move.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Running, Run(rig, move, 0.5f), "On its way");
            target.position = new Vector3(0f, 0f, 6f);

            Assert.AreEqual(TaskStatus.Success, Run(rig, move, 5f), "Reach Position");
            Assert.LessOrEqual(Vector3.Distance(rig.Actuator.Position, target.position), stop + EPSILON,
                "Where the object went, not where it was");
        }

        [Test]
        public void MoveTo_TheVariableHoldsNoObject_OrItIsGone_NoPath()
        {
            GraphRig rig = NewRig();

            Assert.AreEqual(TaskStatus.Failure, new MoveToTask { Target = TARGET }.Tick(rig.Context, DT),
                "Nothing to walk to");

            Transform target = BindObject(rig, new Vector3(5f, 0f, 0f));
            var move = new MoveToTask { Target = TARGET };
            Assert.AreEqual(TaskStatus.Running, move.Tick(rig.Context, DT));
            Object.DestroyImmediate(target.gameObject);

            Assert.AreEqual(TaskStatus.Failure, move.Tick(rig.Context, DT), "The object is gone on the way");
        }

        [Test]
        public void MoveTo_NoWayThere_NoPathAtOnce()
        {
            GraphRig rig = NewRig(navigator: new NoWayNavigator());

            Assert.AreEqual(TaskStatus.Failure, new MoveToTask(new Vector3(5f, 0f, 0f)).Tick(rig.Context, DT));
        }

        // ---------- helpers ----------

        private static GraphRig NewRig(Vector3 at = default, INavigator navigator = null)
        {
            var rig = new GraphRig(trace: false, navigator: navigator);
            rig.Actuator.Position = at;
            return rig;
        }

        /// <summary>An awake agent of the world (the places keep the requests of awake agents only).</summary>
        private static GraphRig RigIn(IAIWorld world, Vector3 at, INavigator navigator = null)
        {
            var rig = new GraphRig(trace: false, world: world, navigator: navigator);
            rig.Agent.gameObject.SetActive(true);
            PutAt(rig, at);
            return rig;
        }

        /// <summary>Body and object of the agent stand together, as on a character (places measure the object).</summary>
        private static void PutAt(GraphRig rig, Vector3 at)
        {
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
        }

        private Transform NewObject(Vector3 at)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        /// <summary>The object to fight, with the number of attack places it gives (its Attack Slots).</summary>
        private Transform NewTarget(Vector3 at, int places)
        {
            Transform target = NewObject(at);
            target.gameObject.AddComponent<AttackSlotCapacity>().Slots = places;
            return target;
        }

        /// <summary>An object in the agent's Target variable, as a sensor or a binding puts it there.</summary>
        private Transform BindObject(GraphRig rig, Vector3 at)
        {
            Transform target = NewObject(at);
            rig.Agent.Blackboard.Set(TARGET, target);
            return target;
        }

        /// <summary>The agent takes a place with Take Attack Slot (the group answers between the steps).</summary>
        private static void GivePlace(IAIWorld world, GraphRig rig, Transform target)
        {
            rig.Agent.Blackboard.Set(TARGET, target);
            var take = new TakeAttackSlotTask { Target = TARGET };
            TaskStatus answer = take.Tick(rig.Context, DT);
            for (int i = 0; i < 10 && answer == TaskStatus.Running; i++)
            {
                world.BeginStep(DT);
                answer = take.Tick(rig.Context, DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer, "Set-up: the agent took a place");
        }

        /// <summary>
        /// Ticks the task the way its state does, step after step, the body doing what it was told in between, until
        /// the task finishes or the time is out; eachTick looks at the body right after a tick, before it moves.
        /// </summary>
        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, Action eachTick = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                answer = task.Tick(rig.Context, DT);
                eachTick?.Invoke();
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        /// <summary>
        /// Ticks the task of every rig side by side, the way a world of agents runs: the world's step, then each of
        /// them ticks and its body moves. They all keep ticking to the end — an agent that stands on its place still
        /// asks for it. Every one of them must have finished (In Place) by the time the seconds are out.
        /// </summary>
        private static void RunTogether(IAIWorld world, IReadOnlyList<GraphRig> rigs, AgentTask task, float seconds)
        {
            var answers = new TaskStatus[rigs.Count];
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int step = 0; step < steps; step++)
            {
                world.BeginStep(DT);
                for (int i = 0; i < rigs.Count; i++)
                {
                    answers[i] = task.Tick(rigs[i].Context, DT);
                    rigs[i].Actuator.Apply(DT);
                }
            }

            for (int i = 0; i < rigs.Count; i++)
                Assert.AreEqual(TaskStatus.Success, answers[i], $"Agent {i}: In Place");
        }

        // Where the offset points, as an angle (degrees) around the target from world forward
        private static float Bearing(Vector3 fromTarget)
        {
            Vector3 flat = Flat(fromTarget);
            return Mathf.Repeat(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 360f);
        }

        // A flanker from the start to its place: how near the target it comes while it is still on the near half
        private float ClosestOnTheNearHalf(IAIWorld world, Transform target, Vector3 start, float stop,
            float bypassRadius, out TaskStatus answer)
        {
            GraphRig flanker = RigIn(world, start);
            flanker.Agent.Blackboard.Set(TARGET, target);
            var flank = new FlankTask { Target = TARGET, StopDistance = stop, BypassRadius = bypassRadius };
            float closest = float.MaxValue;
            answer = Run(flanker, flank, 8f, () =>
            {
                if (flanker.Actuator.Position.x < target.position.x)
                    closest = Mathf.Min(closest, FlatDistance(flanker.Actuator.Position, target.position));
            });

            return closest;
        }

        private static void AssertGoesStraight(GraphRig rig, FlankTask flank, Transform target, string who)
        {
            Vector3 from = rig.Actuator.Position - target.position;
            float stop = flank.StopDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, flank, 5f), $"{who}: In Place");

            Vector3 at = rig.Actuator.Position - target.position;
            Assert.Less(Vector3.Angle(from, at), 1f, $"{who}: straight at the target, from where it stood");
            Assert.That(at.magnitude, AtStopDistance(rig, stop), $"{who}: at Stop Distance");
        }

        // A body that walks up and stops the moment it is near enough stands within one stride inside the distance
        private static IResolveConstraint AtStopDistance(GraphRig rig, float distance) =>
            Is.InRange(distance - Stride(rig) - EPSILON, distance + EPSILON);

        private static float Stride(GraphRig rig) => rig.Actuator.Speed * DT;

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
