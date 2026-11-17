using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static AIKit.Tests.MovementCoverageRigs;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The blocks a fighter waits for its place with (step 90 recheck): Square Off and Step keep to their steps however
    /// fast the body came in (handbook §20б.4 п. 6 — a sidestep, a step in, a step back; a wall check measures the step's
    /// own pace), Step plans its legs the way Direction says and lasts between Time and Time Max by the agent's own
    /// random, and a waiter that loses its target takes its request for a place back (§37.3 — leaving gives up what was
    /// asked). Circle Target stops now and then, never at its first change, the same walk for the same seed.
    /// </summary>
    public sealed class MovementWaitingStepTests
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

        // A waiter south of the target at this radius, one other agent holding the only place from the north; the body
        // is carried on by the run it came in with for the first moments (the leftover glide of a run: 4 m/s)
        private GraphRig WaiterEnteredAtARun(out AIWorld world, out Transform target, float radius, float carried = 0.2f)
        {
            world = new AIWorld();
            target = NewTarget(_made, Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f), DT);
            GraphRig rig = RigIn(world, new Vector3(0f, 0f, -radius), target);
            float time = 0f;
            rig.Actuator.Applying += dt =>
            {
                if (time < carried)
                    rig.Actuator.Position += Vector3.right * (4f * dt);
                time += dt;
            };
            return rig;
        }

        [Test]
        public void SquareOff_EnteredAtARun_KeepsToEachStep_NoSideFlipEveryFewTicks()
        {
            // The run's 4 m/s was the pace its wall check compared every later wary step (1.6 m/s) with: each step read
            // as held by a wall, and the step flipped side every 0.16 s for as long as the fighter waited
            const float RADIUS = 5.5f;
            const float WATCH = 6f;
            GraphRig rig = WaiterEnteredAtARun(out AIWorld world, out Transform target, RADIUS);
            var square = new SquareOffTask { Target = TARGET, Radius = RADIUS, SpeedScale = 0.4f };
            int last = -1, changes = 0, quickFlips = 0;
            float since = 0f;

            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                float time = i * DT;
                Assert.AreEqual(TaskStatus.Running, square.Tick(rig.Context, DT));
                int kind = StepKind(rig.Actuator.Body.Velocity, target.position - rig.Actuator.Position);
                if (kind != last && last >= 0)
                {
                    if (time > 0.5f)
                    {
                        changes++;
                        bool sideFlip = (kind == 1 && last == 2) || (kind == 2 && last == 1);
                        if (sideFlip && since < 0.3f)
                            quickFlips++;
                    }

                    since = 0f;
                }

                since += DT;
                last = kind;
                Apply(rig, DT);
                world.BeginStep(DT);
            }

            Assert.AreEqual(0, quickFlips, $"A sidestep turned back within 0.3 s {quickFlips} times: each step read as blocked");
            Assert.LessOrEqual(changes, 12, "Steps of 0.3–2 s: a dozen changes in 6 s at the most");
        }

        [Test]
        public void Step_EnteredAtARun_TurnsAwayAtMostOnce()
        {
            const float RADIUS = 5.5f;
            GraphRig rig = WaiterEnteredAtARun(out AIWorld world, out Transform target, RADIUS);
            var step = new StepTask
            {
                Target = TARGET, Direction = StepDirection.Aside, Speed = 0.4f, Time = 3f, TimeMax = 3f, Radius = RADIUS,
                Band = 1f
            };
            int last = -1, changes = 0;
            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 400 && status == TaskStatus.Running; i++)
            {
                status = step.Tick(rig.Context, DT);
                int kind = StepKind(rig.Actuator.Body.Velocity, target.position - rig.Actuator.Position);
                if (status == TaskStatus.Running && kind != last && last >= 0)
                    changes++;
                last = kind;
                Apply(rig, DT);
                world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreEqual(StepEnd.TimeUp, step.LastEndFor(rig.Context), "Only its time ends a step");
            Assert.LessOrEqual(changes, 3, "The run's pace costs one turn away at most, then each leg its own pace");
        }

        private Transform TargetAt(Vector3 at)
        {
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(target);
            target.transform.position = at;
            return target.transform;
        }

        [Test]
        public void Step_NoRingAroundTheTarget_OnlyItsTimeEndsASidestep()
        {
            // No world: no places, no room round a place — a sidestep has no goal, only its time ends it
            Transform target = TargetAt(new Vector3(0f, 0f, 4f));
            AgentContext context = Bare(Vector3.zero, target);
            TestActuator body = BodyOf(context);
            var step = new StepTask { Direction = StepDirection.Aside, Time = 1f, TimeMax = 1f, Speed = 0.4f, Radius = 4f, Band = 1f };

            var kinds = new HashSet<int>();
            TaskStatus status = TaskStatus.Running;
            for (int ticks = 0; ticks < 200 && status == TaskStatus.Running; ticks++)
            {
                status = step.Tick(context, DT);
                if (status == TaskStatus.Running)
                    kinds.Add(StepKind(body.Body.Velocity, target.position - body.Position));
                body.Apply(DT);
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreEqual(1, kinds.Count, "One direction for the whole step");
            Assert.AreEqual(StepEnd.TimeUp, step.LastEndFor(context));
            Assert.That(step.LastLastedFor(context), Is.InRange(1f, 1f + DT + 1e-4f));
        }

        [Test]
        public void Step_DirectionLeft_FirstLegLeft_ThenBackRight()
        {
            const float RADIUS = 4f;
            var world = new AIWorld();
            Transform target = NewTarget(_made, Vector3.zero, 1);
            GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f), DT);
            GraphRig other = RigIn(world, AttackSlots.DirectionOf(120f) * RADIUS, target);
            GraphRig rig = RigIn(world, AttackSlots.DirectionOf(240f) * RADIUS, target);
            // Three on the ring: the places are 120° apart, the room round each 30° (a quarter of the gap)
            AttackSlots.PlaceAround(other.Context, target, out _);
            AttackSlots.PlaceAround(rig.Context, target, out float place);
            rig.Actuator.Position = rig.Agent.transform.position = AttackSlots.DirectionOf(place) * RADIUS;
            var step = new StepTask { Target = TARGET, Direction = StepDirection.Left, Time = 6f, TimeMax = 6f, Radius = RADIUS };

            var legs = new List<int>();
            float turnedAt = float.NaN;
            for (int i = 0; i < Mathf.CeilToInt(6f / DT); i++)
            {
                AttackSlots.PlaceAround(other.Context, target, out _);
                if (step.Tick(rig.Context, DT) != TaskStatus.Running)
                    break;

                int kind = StepKind(rig.Actuator.Body.Velocity, target.position - rig.Actuator.Position);
                if (legs.Count == 0 || legs[^1] != kind)
                {
                    if (legs.Count == 1)
                    {
                        Vector3 from = rig.Actuator.Position - target.position;
                        float bearing = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
                        AttackSlots.PlaceAround(rig.Context, target, out float now);
                        turnedAt = Mathf.Abs(Mathf.DeltaAngle(now, bearing));
                    }

                    legs.Add(kind);
                }

                Apply(rig, DT);
                world.BeginStep(DT);
            }

            Assert.That(legs.Count, Is.GreaterThanOrEqualTo(2), "It walks leg after leg");
            Assert.AreEqual(1, legs[0], "The first leg goes the way Direction says: left");
            Assert.AreEqual(2, legs[1], "…the next one back the other way");
            Assert.That(turnedAt, Is.InRange(12f - 1f, 24f + 1f), "The first leg ends 0.4–0.8 of its 30° room past its place");
        }

        [Test]
        public void Step_DirectionOut_FromTheMiddle_FirstLegOutThenIn()
        {
            const float RADIUS = 4f;
            const float BAND = 1f;
            Transform target = TargetAt(new Vector3(0f, 0f, RADIUS));
            AgentContext context = Bare(Vector3.zero, target);
            TestActuator body = BodyOf(context);
            var step = new StepTask { Direction = StepDirection.Out, Time = 6f, TimeMax = 6f, Radius = RADIUS, Band = BAND };

            var legs = new List<int>();
            var turns = new List<float>();
            for (int i = 0; i < Mathf.CeilToInt(6f / DT); i++)
            {
                if (step.Tick(context, DT) != TaskStatus.Running)
                    break;

                int kind = StepKind(body.Body.Velocity, target.position - body.Position);
                if (legs.Count == 0 || legs[^1] != kind)
                {
                    legs.Add(kind);
                    turns.Add(Flat(target.position - body.Position).magnitude);
                }

                body.Apply(DT);
            }

            Assert.That(legs.Count, Is.GreaterThanOrEqualTo(3));
            Assert.AreEqual(4, legs[0], "Out first, as Direction says");
            Assert.AreEqual(3, legs[1], "…then back in");
            Assert.That(turns[1], Is.InRange(RADIUS + 0.4f * BAND - 0.1f, RADIUS + 0.8f * BAND + 0.1f),
                "The step out ends in the far half of the band (0.4–0.8 of it past the radius)");
            Assert.That(turns[2], Is.InRange(RADIUS - 0.8f * BAND - 0.1f, RADIUS - 0.4f * BAND + 0.1f),
                "…the step in in the near half");
        }

        // How long each of ten steps lasted on this seed
        private List<float> TenSteps(int seed)
        {
            Transform target = TargetAt(new Vector3(0f, 0f, 4f));
            AgentContext context = Bare(Vector3.zero, target, seed);
            var step = new StepTask { Direction = StepDirection.Aside, Time = 0.8f, TimeMax = 1.6f, Radius = 4f, Band = 1f };
            var lasted = new List<float>();
            for (int run = 0; run < 10; run++)
            {
                TaskStatus status = TaskStatus.Running;
                for (int i = 0; i < 200 && status == TaskStatus.Running; i++)
                {
                    status = step.Tick(context, DT);
                    context.Actuator.Apply(DT);
                }

                lasted.Add(step.LastLastedFor(context));
            }

            return lasted;
        }

        [Test]
        public void Step_TimeToTimeMax_EachStepBetween_SameSeedSameTimes()
        {
            List<float> first = TenSteps(1234);
            List<float> again = TenSteps(1234);
            List<float> other = TenSteps(99);

            Assert.That(first, Is.All.InRange(0.8f, 1.6f + DT + 1e-4f), "Each step between Time and Time Max");
            CollectionAssert.AreEqual(first, again, "The same seed: the same times");
            Assert.That(first.Distinct().Count(), Is.GreaterThan(1), "Not all the same");
            CollectionAssert.AreNotEqual(first, other, "Another seed: other times");
        }

        // A waiter asks for the only place, then its target is gone (the variable cleared): the block fails "Lost"
        private void LosesTheTargetWhileWaiting(AgentTask waiting)
        {
            var world = new AIWorld();
            Transform target = NewTarget(_made, Vector3.zero, 1);
            GraphRig holder = GivePlaceTo(world, target, new Vector3(0f, 0f, 1.5f), DT);
            GraphRig waiter = RigIn(world, new Vector3(0f, 0f, -4f), target);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(TaskStatus.Running, waiting.Tick(waiter.Context, DT), "Every place is taken: it waits");
                world.BeginStep(DT);
            }

            waiter.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.AreEqual(TaskStatus.Failure, waiting.Tick(waiter.Context, DT), "Lost");

            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            for (int i = 0; i < 3; i++)
                world.BeginStep(DT);

            Assert.IsFalse(Holds(world, target, waiter), "A waiter that left gets no place: its request went with it");
            Assert.IsFalse(Holds(world, target, holder), "…and the freed place stays free");
        }

        [Test]
        public void SquareOff_LostTheTargetWhileWaiting_ItsRequestIsWithdrawn() =>
            LosesTheTargetWhileWaiting(new SquareOffTask { Target = TARGET });

        [Test]
        public void CircleTarget_LostTheTargetWhileWaiting_ItsRequestIsWithdrawn() =>
            LosesTheTargetWhileWaiting(new CircleTargetTask { Target = TARGET, WaitForSlot = true });

        [Test]
        public void CircleTarget_StopsNowAndThen_NeverAtItsFirstChange()
        {
            const float WATCH = 60f;
            Transform target = TargetAt(Vector3.zero);
            AgentContext context = Bare(new Vector3(0f, 0f, -4f), target, 77);
            TestActuator body = BodyOf(context);
            var circle = new CircleTargetTask
            {
                WaitForSlot = false, Duration = WATCH + 1f, ChangeEvery = 1.5f, Sway = 1.2f, Radius = 4f
            };

            int stops = 0;
            float standing = 0f, firstStop = float.PositiveInfinity;
            bool wasStanding = false;
            for (int i = 0; i < Mathf.CeilToInt(WATCH / DT); i++)
            {
                Assert.AreEqual(TaskStatus.Running, circle.Tick(context, DT));
                bool stands = body.Body.Velocity.sqrMagnitude < 1e-6f;
                if (stands)
                {
                    standing += DT;
                    firstStop = Mathf.Min(firstStop, i * DT);
                    if (!wasStanding)
                        stops++;
                }

                wasStanding = stands;
                body.Apply(DT);
            }

            Assert.Greater(firstStop, 0.6f, "Never a stop the moment it starts");
            Assert.GreaterOrEqual(stops, 3, "It does stop now and then");
            Assert.That(standing / WATCH, Is.InRange(0.02f, 0.15f),
                "Short stops at a fifth of its changes: a few percent of the time standing");
        }

        // Where the body was at each step of 5 s of circling on this seed
        private List<Vector3> Circling(int seed)
        {
            Transform target = TargetAt(Vector3.zero);
            AgentContext context = Bare(new Vector3(0f, 0f, -4f), target, seed);
            TestActuator body = BodyOf(context);
            var circle = new CircleTargetTask { WaitForSlot = false, Duration = 10f };
            var walk = new List<Vector3>();
            for (int i = 0; i < Mathf.CeilToInt(5f / DT); i++)
            {
                circle.Tick(context, DT);
                body.Apply(DT);
                walk.Add(body.Position);
            }

            return walk;
        }

        [Test]
        public void CircleTarget_SameSeed_SameWalk()
        {
            List<Vector3> first = Circling(5);
            List<Vector3> again = Circling(5);
            List<Vector3> other = Circling(6);

            for (int i = 0; i < first.Count; i++)
                Assert.That(Vector3.Distance(first[i], again[i]), Is.LessThan(1e-5f), $"The same seed, the same walk (step {i})");
            Assert.That(first.Zip(other, Vector3.Distance).Max(), Is.GreaterThan(0.1f), "Another seed walks otherwise");
        }
    }
}
