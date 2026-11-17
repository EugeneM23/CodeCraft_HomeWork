using System.Collections.Generic;
using System.Linq;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// Climbing walls (handbook §13.2–13.4: what an edge asks is the body's, the body executes the edge, one body on a
    /// wall at a time): the Climb To node opens the walls to the character's paths while it runs and is not cut short on
    /// a wall; the Climb decision holds when the target is where only climbing leads; a NavMesh body that climbs keeps
    /// the walls shut until the graph allows them, any other body never walks them; the animation shows the climb from
    /// the body's fact and no walk meanwhile. The body's own climb over a real NavMesh is in the lab (ClimbBodyTests).
    /// </summary>
    public sealed class ClimbTests
    {
        private const string TARGET = "Target";
        private const float STEP = 0.1f;

        private GraphRig _rig;
        private ClimbingNavigator _navigator;
        private GameObject _victim;

        [SetUp]
        public void SetUp()
        {
            _navigator = new ClimbingNavigator();
            _rig = new GraphRig(trace: false, navigator: _navigator) { ThinkInterval = 0f };
            _victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            _victim.transform.position = new Vector3(6f, 2.3f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_victim);
            GraphRig.DestroyAll();
        }

        // ---------- the weapon on a wall ----------

        [Test]
        public void Character_OnAWall_TheWeaponInTheHandGoesOnTheBack_BackInTheHandOffTheWall()
        {
            var sword = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            var inHand = new GameObject("Sword In Hand") { hideFlags = HideFlags.HideAndDontSave };
            var onBack = new GameObject("Sword On Back") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                _rig.GiveWeapon(sword);
                _rig.Carry(sword, inHand, onBack);
                _rig.Step(STEP);
                Assert.IsTrue(inHand.activeSelf, "On the ground: the sword in the hand");
                Assert.IsFalse(onBack.activeSelf);

                foreach (int way in new[] { 1, -1 })
                {
                    _navigator.ClimbDirection = way;
                    _rig.Step(STEP);
                    Assert.IsFalse(inHand.activeSelf, $"On a wall ({way}): both hands for the wall");
                    Assert.IsTrue(onBack.activeSelf, "The sword on the back");
                    Assert.AreSame(sword, _rig.Agent.Weapon, "Still the weapon it fights with");

                    _navigator.ClimbDirection = 0;
                    _rig.Step(STEP);
                    Assert.IsTrue(inHand.activeSelf, "Off the wall: in the hand again");
                    Assert.IsFalse(onBack.activeSelf);
                }
            }
            finally
            {
                Object.DestroyImmediate(inHand);
                Object.DestroyImmediate(onBack);
                Object.DestroyImmediate(sword);
            }
        }

        // ---------- Climb To ----------

        [Test]
        public void ClimbTo_OpensTheWallsWhileItRuns_ShutsThemOnceLeft()
        {
            RunClimbTo(new ClimbToTask { Target = TARGET });
            Assert.IsFalse(_navigator.ClimbAllowed, "Set-up: shut before the node runs");

            Assert.IsTrue(Reaches("Climb To"));

            Assert.IsTrue(_navigator.ClimbAllowed, "While the node runs the paths may run over the walls");
            Assert.AreEqual(_victim.transform.position, _navigator.Destination, "And it goes to the target");

            _rig.Instance.Stop(_rig.Context);

            Assert.IsFalse(_navigator.ClimbAllowed, "Left — the walls are shut again");
        }

        [Test]
        public void ClimbTo_OnAWall_IsNotCutShort_OverItAndOff_Climbed()
        {
            var task = new ClimbToTask { Target = TARGET };
            RunClimbTo(task);
            Assert.IsTrue(Reaches("Climb To"));

            _navigator.ClimbDirection = 1;              // the body has come to the wall and climbs it
            _rig.Step(STEP, 5);
            Assert.AreEqual("Climb To", _rig.StateName);
            Assert.IsTrue(task.CommitsFor(_rig.Context), "A Brain does not take the character off a wall halfway");

            _navigator.ClimbDirection = 0;              // on the top and off the wall: the target is walkable now
            _navigator.Climbs++;
            _navigator.Way = ClimbWay.OnFoot;
            _rig.Step(STEP, 2);                         // the node succeeds, the arrow is taken on the next step

            Assert.AreEqual("Climbed", _rig.StateName, "No wall left between: Climbed — the Brain looks again from up there");
            Assert.IsFalse(task.CommitsFor(_rig.Context));
        }

        [Test]
        public void ClimbTo_NoWallBetween_ClimbedAtOnce()
        {
            _navigator.Way = ClimbWay.OnFoot;
            RunClimbTo(new ClimbToTask { Target = TARGET });
            Assert.IsTrue(Reaches("Climb To"));

            _rig.Step(STEP);                            // the node succeeded on its first run, the arrow is taken now

            Assert.AreEqual("Climbed", _rig.StateName, "The target is walkable: nothing to climb, what comes next walks");
        }

        [Test]
        public void ClimbTo_NoWayEvenOverTheWalls_NoWay()
        {
            _navigator.Unreachable = true;
            RunClimbTo(new ClimbToTask { Target = TARGET });
            Assert.IsTrue(Reaches("Climb To"));

            _rig.Step(STEP);

            Assert.AreEqual("No Way", _rig.StateName);
        }

        [Test]
        public void ClimbTo_ABodyThatDoesNotClimb_NoWay()
        {
            _navigator.CanClimb = false;
            RunClimbTo(new ClimbToTask { Target = TARGET });
            Assert.IsTrue(Reaches("Climb To"));

            _rig.Step(STEP);

            Assert.AreEqual("No Way", _rig.StateName);
        }

        // ---------- the Climb decision ----------

        [Test]
        public void Climb_TheTargetOnlyByClimbing_IsTaken_OnceReachableOnFoot_ChaseInstead()
        {
            BrainGraph();
            _navigator.Way = ClimbWay.OnlyByClimbing;
            Run();

            Assert.IsTrue(Reaches("Climb Block"), "The target is up where only climbing leads: Climb");

            _navigator.Way = ClimbWay.OnFoot;

            Assert.IsTrue(Reaches("Chase Block"), "Walkable now (over the wall, or the target came down): Chase");
            Assert.AreEqual("the target can be reached on foot", WhyNot("Climb"));
        }

        [Test]
        public void Climb_OnAWall_HoldsToTheEndOfTheClimb_WhateverTheWay()
        {
            BrainGraph();
            _navigator.Way = ClimbWay.OnlyByClimbing;
            Run();
            Assert.IsTrue(Reaches("Climb Block"));

            _navigator.ClimbDirection = 1;
            _navigator.Way = ClimbWay.OnFoot;           // the answer changes while the body is on the wall
            _rig.Step(STEP, 10);

            Assert.AreEqual("Climb Block", _rig.StateName, "A climb is never left halfway");
        }

        [Test]
        public void Climb_NoWayAtAll_DoesNotHold_AndSaysWhy()
        {
            BrainGraph();
            _navigator.Way = ClimbWay.NoWay;
            Run();

            Assert.IsTrue(Reaches("Chase Block"));
            Assert.AreEqual("no way to the target, even climbing", WhyNot("Climb"));
        }

        [Test]
        public void Climb_ABodyThatDoesNotClimb_NeverTakesIt()
        {
            BrainGraph();
            _navigator.CanClimb = false;
            _navigator.Way = ClimbWay.OnlyByClimbing;
            Run();

            Assert.IsTrue(Reaches("Chase Block"));
            Assert.AreEqual("the body does not climb", WhyNot("Climb"));
        }

        // ---------- the NavMesh body ----------

        [Test]
        public void NavMeshBody_ClimbsWalls_TheWallsAreShutUntilAllowed()
        {
            LogAssert.ignoreFailingMessages = true;     // a NavMeshAgent without a baked mesh complains
            NavMeshBody body = MakeBody(new NavMeshBody { ClimbsWalls = true }, out GameObject go, out NavMeshAgent agent);
            try
            {
                Assert.IsNotNull(body.Climber);
                Assert.IsTrue(body.Pathing.CanClimb);
                Assert.IsTrue(body.Mover.Body.CanClimb, "The body says it climbs (handbook §13.2)");
                Assert.Zero(agent.areaMask & (1 << NavMeshLinkClimber.CLIMB_AREA), "Shut until the graph allows them");

                body.Pathing.ClimbAllowed = true;
                Assert.NotZero(agent.areaMask & (1 << NavMeshLinkClimber.CLIMB_AREA));

                body.Pathing.ClimbAllowed = false;
                Assert.Zero(agent.areaMask & (1 << NavMeshLinkClimber.CLIMB_AREA));
            }
            finally
            {
                Object.DestroyImmediate(go);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void NavMeshBody_DoesNotClimb_NeverWalksAWall_EvenWhenAllowed()
        {
            LogAssert.ignoreFailingMessages = true;
            NavMeshBody body = MakeBody(new NavMeshBody(), out GameObject go, out NavMeshAgent agent);
            try
            {
                Assert.IsNull(body.Climber);
                Assert.IsFalse(body.Pathing.CanClimb);
                Assert.Zero(agent.areaMask & (1 << NavMeshLinkClimber.CLIMB_AREA), "An agent walks every area by default");

                body.Pathing.ClimbAllowed = true;
                Assert.Zero(agent.areaMask & (1 << NavMeshLinkClimber.CLIMB_AREA));
                Assert.AreEqual(0, body.ClimbDirection);
            }
            finally
            {
                Object.DestroyImmediate(go);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        // (The climb shown by the animator: AIAnimatorTests.Climb_OnAWall_*)

        // ---------- the rig ----------

        private static NavMeshBody MakeBody(NavMeshBody body, out GameObject go, out NavMeshAgent agent)
        {
            go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            agent = go.AddComponent<NavMeshAgent>();
            AICharacter character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            return body;
        }

        // Brain → Daily Routine → Do → Climb To (its own target); Climbed → a long Wait "Climbed", No Way → "No Way"
        private void RunClimbTo(ClimbToTask task)
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            AgentState climb = g.Block(task, "Climb To");
            g.Do(g.Always(), climb);
            g.Then(climb, g.Block(new WaitTask(100f), "Climbed"));
            g.OnFail(climb, g.Block(new WaitTask(100f), "No Way"));
            _rig.SetGraph(g.Build());
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
        }

        private BrainGraphBuilder _graph;

        // Brain (Target) → Climb (1) → Climb Block; Chase (2) → Chase Block
        private void BrainGraph()
        {
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _graph.Do(_graph.Decision(new ClimbReasoner(), 1), _graph.Block(new WaitTask(100f), "Climb Block"));
            _graph.Do(_graph.Decision(new ChaseReasoner(), 2),
                _graph.Block(new WaitTask(100f), "Chase Block"));
        }

        private void Run()
        {
            _rig.Context.ExplainsDecisions = true;
            _rig.SetGraph(_graph.Build());
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
        }

        private bool Reaches(string state, float seconds = 3f) => _rig.StepUntil(() => _rig.StateName == state, seconds, STEP);

        private string WhyNot(string decision) =>
            CombatBrainTask.VerdictsOf(_graph.Brain, _rig.Context).Single(verdict => verdict.State.DisplayName == decision).WhyNot;

        /// <summary>A navigator whose walls are the test's: how the target can be reached, and the body on a wall or not.</summary>
        private sealed class ClimbingNavigator : INavigator, IClimbingNavigator
        {
            public ClimbWay Way = ClimbWay.OnlyByClimbing;
            public bool Unreachable;

            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Status.IsMoving() ? 1f : Mathf.Infinity;

            public NavEdgeKind CurrentEdge => ClimbDirection != 0 ? NavEdgeKind.Ladder : NavEdgeKind.Walk;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                Status = Unreachable ? NavStatus.Unreachable : NavStatus.Following;
                return Unreachable ? NavVerdict.Unreachable : NavVerdict.Reachable;
            }

            public void Clear() => Status = NavStatus.Idle;

            public void Step(float dt)
            {
            }

            public bool CanClimb { get; set; } = true;

            public bool ClimbAllowed { get; set; }

            public int ClimbDirection { get; set; }

            public int Climbs { get; set; }

            public ClimbWay WayTo(Vector3 point) => Way;
        }
    }
}
