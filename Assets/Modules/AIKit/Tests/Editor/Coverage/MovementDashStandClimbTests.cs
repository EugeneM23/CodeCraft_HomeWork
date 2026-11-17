using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Dash, Stand and Climb To (step 90 recheck): a dash's way is relative to the target in every direction; a body that
    /// cannot dash, or holds no weapon with that dash, is Blocked and nothing is shown; during the dash the eyes stay on
    /// the target and the body is the dash's; Stand looks at the Brain's target; Climb To opens the walls again if a block
    /// before shut them.
    /// </summary>
    public sealed class MovementDashStandClimbTests
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
        }

        private Transform TargetAt(Vector3 at)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private WeaponAsset WithDash(DashDirection direction, float distance, float time)
        {
            var weapon = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            _made.Add(weapon);
            weapon.Dashes.Add(new WeaponDash { Direction = direction, Distance = distance, Time = time, Action = "Dash Back" });
            return weapon;
        }

        [Test]
        public void Dash_Way_EveryDirectionRelativeToTheTarget()
        {
            var inward = new Vector3(0f, 0f, 1f);
            const float D = 0.70710678f;
            var expected = new Dictionary<DashDirection, Vector3>
            {
                [DashDirection.Back] = new(0f, 0f, -1f),
                [DashDirection.Forward] = new(0f, 0f, 1f),
                [DashDirection.Left] = new(-1f, 0f, 0f),
                [DashDirection.Right] = new(1f, 0f, 0f),
                [DashDirection.BackLeft] = new(-D, 0f, -D),
                [DashDirection.BackRight] = new(D, 0f, -D),
                [DashDirection.ForwardLeft] = new(-D, 0f, D),
                [DashDirection.ForwardRight] = new(D, 0f, D)
            };

            foreach (KeyValuePair<DashDirection, Vector3> way in expected)
            {
                Assert.That(Vector3.Distance(DashTask.Way(way.Key, inward), way.Value), Is.LessThan(1e-5f),
                    $"{way.Key}: {DashTask.Way(way.Key, inward)}");
            }
        }

        [Test]
        public void Dash_ABodyThatCannotDash_OrNoWeapon_Blocked_NothingShown()
        {
            // A body with no dash of its own (a Simple Body's mover)
            var go = new GameObject("Simple") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = new SimpleBody();
            character.Initialize();
            AgentContext simple = character.Context;
            simple.BrainTarget = TargetAt(new Vector3(0f, 0f, 4f));
            simple.Weapon = WithDash(DashDirection.Back, 2f, 0.5f);
            var watch = new CharacterActionsWatch(simple);
            Vector3 at = go.transform.position;

            Assert.AreEqual(TaskStatus.Failure, new DashTask().Tick(simple, DT), "A body that cannot dash: Blocked");
            simple.Actuator.Apply(DT);
            CollectionAssert.IsEmpty(watch.Played, "Nothing is shown");
            Assert.AreEqual(at, go.transform.position, "The body did not move");

            // A body that can dash, with no weapon in the hand
            var body = new TestActuator();
            var bare = new AgentContext(null, null, 1) { Actuator = body, BrainTarget = TargetAt(new Vector3(0f, 0f, 4f)) };
            var bareWatch = new CharacterActionsWatch(bare);
            Assert.AreEqual(TaskStatus.Failure, new DashTask().Tick(bare, DT), "No weapon, no dash: Blocked");
            Assert.AreEqual(0, body.Dashes);
            CollectionAssert.IsEmpty(bareWatch.Played);
        }

        [Test]
        public void Dash_DuringTheDash_TheEyesStayOnTheTarget_TheMoveAxisIsTheDashs()
        {
            const float TIME = 0.5f;
            var body = new TestActuator();
            Transform target = TargetAt(new Vector3(0f, 0f, 4f));
            var context = new AgentContext(null, null, 1)
            {
                Actuator = body, BrainTarget = target, Weapon = WithDash(DashDirection.Back, 2f, TIME)
            };
            var dash = new DashTask { Direction = DashDirection.Back };
            object other = new object();

            TaskStatus status = dash.Tick(context, DT);
            int ticks = 0;
            while (status == TaskStatus.Running && ticks++ < 100)
            {
                body.Apply(DT);
                status = dash.Tick(context, DT);
                if (status != TaskStatus.Running)
                    break;

                Assert.IsFalse(context.Arbiter.TryClaim(other, BodyAxis.Move, MoveAuthority.Strafe),
                    "Nobody else moves the body during the dash");
                Vector3 to = target.position - body.Position;
                to.y = 0f;
                Assert.Less(Vector3.Angle(body.Facing, to), 1f, "The eyes stay on the target");
            }

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            Assert.That(ticks * DT, Is.EqualTo(TIME).Within(DT * 1.5f), "As long as the dash");
            Assert.IsTrue(context.Arbiter.TryClaim(other, BodyAxis.Move, MoveAuthority.Strafe), "The dash over: the body is free");
        }

        [Test]
        public void Stand_NoTargetOfItsOwn_FacesTheBrainsTarget()
        {
            var body = new TestActuator();                                   // faces +X
            var context = new AgentContext(null, null, 1) { Actuator = body, BrainTarget = TargetAt(new Vector3(0f, 0f, 5f)) };
            var stand = new StandTask { Time = 0.5f };

            TaskStatus status = stand.Tick(context, DT);
            Assert.Less(Vector3.Angle(body.Facing, Vector3.forward), 0.01f, "It looks at the Brain's target at once");
            int ticks = 1;
            while (status == TaskStatus.Running && ticks < 100)
            {
                status = stand.Tick(context, DT);
                ticks++;
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.That(ticks * DT, Is.EqualTo(0.5f).Within(DT * 1.5f), "Done after its time");
            Assert.AreEqual(ticks, body.Stops, "Stopped every tick: it stands");
        }

        /// <summary>A navigator whose walls the test sets: the target only by climbing, the walls as allowed.</summary>
        private sealed class Walls : INavigator, IClimbingNavigator
        {
            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Status.IsMoving() ? 1f : Mathf.Infinity;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                Status = NavStatus.Following;
                return NavVerdict.Reachable;
            }

            public void Clear() => Status = NavStatus.Idle;

            public void Step(float dt)
            {
            }

            public bool CanClimb => true;

            public bool ClimbAllowed { get; set; }

            public int ClimbDirection => 0;

            public int Climbs => 0;

            public ClimbWay WayTo(Vector3 point) => ClimbWay.OnlyByClimbing;
        }

        [Test]
        public void ClimbTo_APreviousBlockShutTheWalls_ItOpensThemAgain()
        {
            var walls = new Walls();
            var context = new AgentContext(null, null, 1)
            {
                Actuator = new TestActuator(), Navigation = walls, BrainTarget = TargetAt(new Vector3(0f, 2f, 5f))
            };
            var climb = new ClimbToTask();

            Assert.AreEqual(TaskStatus.Running, climb.Tick(context, DT));
            Assert.IsTrue(walls.ClimbAllowed, "The walls are open while it runs");

            walls.ClimbAllowed = false;                                    // shut from elsewhere meanwhile
            Assert.AreEqual(TaskStatus.Running, climb.Tick(context, DT));
            Assert.IsTrue(walls.ClimbAllowed, "Opened again on its next tick");

            climb.Interrupt(context);
            Assert.IsFalse(walls.ClimbAllowed, "Shut once it is left");
        }
    }
}
