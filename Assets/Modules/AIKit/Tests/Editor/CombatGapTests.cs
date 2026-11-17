using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The corners of the fight: the melee chaser waiting round the target while it has no place to attack from, an
    /// ability that waits for its token, the odd requests and pools of the tokens, the roster's housekeeping, and
    /// abilities asked about what is not there.
    /// </summary>
    public class CombatGapTests
    {
        private const float DT = 0.02f;
        private const string POOL = "Melee";

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
        }

        private sealed class Capacity : MonoBehaviour, ITokenCapacity
        {
            public int Value;

            public int CapacityOf(string pool) => Value;
        }

        private sealed class Perceivable : MonoBehaviour, IPerceivable
        {
            public bool Alive = true;

            public bool IsPerceivable => Alive;

            public int Faction => 0;

            public Vector3 AimPoint => transform.position;
        }

        private Transform NewTarget(Vector3 position)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>A rig in a world whose agent is awake, so the roster counts it.</summary>
        private GraphRig RigIn(AIWorld world, Vector3 at = default)
        {
            var rig = new GraphRig(trace: false, world: world);
            _created.Add(rig.Agent.gameObject);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            return rig;
        }

        // The world's step first, as the game's loop runs it (the requests for places are answered there), then the agent
        private static void StepWithTheWorld(GraphRig rig, AIWorld world, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                world.BeginStep(DT);
                rig.Step(DT);
            }
        }

        // ---------- the melee chaser and its places ----------

        [Test]
        public void MeleeChaser_NoPlaceToAttackFrom_CirclesTheTarget_ThenChasesWhenOneIsFree()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world);
            Transform target = rig.CreateTargetAt(new Vector3(6f, 0f, 0f));
            Capacity capacity = target.gameObject.AddComponent<Capacity>();
            rig.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning { TokenPool = POOL }));

            StepWithTheWorld(rig, world, 0.5f);
            Assert.AreEqual("Circle Target", rig.StateName, "No place can come: it waits round the target");

            capacity.Value = 1;
            StepWithTheWorld(rig, world, 0.5f);
            Assert.AreEqual("Chase", rig.StateName, "A place is free: it closes in");
            rig.DestroyTarget();
        }

        // ---------- an ability with a token ----------

        [Test]
        public void UseAbility_WithATokenPool_WaitsForTheToken_PlaysIt_AndGivesItBack()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            TestAbilityRunner swing = rig.AddAbility("Swing", duration: 0.2f);
            rig.Abilities.DefinitionOf("Swing").TokenPool = POOL;
            var task = new UseAbilityTask(GraphRig.Named("Swing"));

            Assert.AreEqual(TaskStatus.Running, task.Tick(rig.Context, DT));
            Assert.AreEqual("Swing", task.WaitingForBy(rig.Context), "Filed, waiting");
            Assert.AreEqual(0, swing.Starts);

            world.BeginStep(DT);                                    // the request matures
            Assert.AreEqual(TaskStatus.Running, task.Tick(rig.Context, DT));
            Assert.AreEqual(1, swing.Starts, "Granted: it plays");
            Assert.IsNull(task.WaitingForBy(rig.Context));
            Assert.IsTrue(rig.Context.HoldsToken(POOL));

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 50 && status == TaskStatus.Running; i++)
            {
                rig.Tick(DT);
                status = task.Tick(rig.Context, DT);
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.IsFalse(rig.Context.HoldsToken(POOL), "The token went back");
            rig.DestroyTarget();
        }

        [Test]
        public void UseAbility_TargetOutOfTheTokensReach_Refused()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world);
            rig.CreateTargetAt(new Vector3(30f, 0f, 0f));
            TestAbilityRunner swing = rig.AddAbility("Swing", maxRange: 100f);
            rig.Abilities.DefinitionOf("Swing").TokenPool = POOL;
            var task = new UseAbilityTask(GraphRig.Named("Swing"));

            Assert.AreEqual(TaskStatus.Failure, task.Tick(rig.Context, DT));
            Assert.AreEqual(0, swing.Starts);
            Assert.IsNull(task.WaitingForBy(rig.Context));
            rig.DestroyTarget();
        }

        // ---------- tokens and the roster ----------

        [Test]
        public void Tokens_FiledRequestThatFallsOutOfReach_IsWithdrawn()
        {
            var world = new AIWorld();
            GraphRig rig = RigIn(world);
            Transform target = NewTarget(Vector3.zero);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;

            Assert.AreEqual(TokenResult.Pending, tokens.Request(rig.Agent, POOL, 1, 0f, 1f));
            Assert.AreEqual(TokenResult.Refused, tokens.Request(rig.Agent, POOL, 1, 0f, tokens.WinRadius + 1f));
            world.Roster.Step(DT);

            Assert.IsFalse(tokens.Holds(rig.Agent, POOL), "Not granted later from the old request");
            Assert.IsEmpty(tokens.Pools);
        }

        [Test]
        public void Tokens_Pools_TheOnesAskedForOrHeld_EachOnce()
        {
            var world = new AIWorld();
            GraphRig a = RigIn(world);
            GraphRig b = RigIn(world);
            AttackTokens tokens = world.Roster.EngagementOf(NewTarget(Vector3.zero)).Tokens;

            tokens.Request(a.Agent, POOL, 1, 0f, 1f);
            tokens.Request(b.Agent, POOL, 1, 0f, 1f);
            tokens.Request(a.Agent, "Ranged", 1, 0f, 1f);

            CollectionAssert.AreEquivalent(new[] { POOL, "Ranged" }, tokens.Pools.ToList());
        }

        [Test]
        public void Roster_Housekeeping_NullIgnored_DestroyedDropped()
        {
            var roster = new AgentRoster();
            var rig = new GraphRig(trace: false);
            roster.Add(rig.Agent);
            roster.Remove(null);
            Assert.AreEqual(1, roster.Count);
            Assert.IsFalse(roster.AllyAhead(null, Vector3.zero, Vector3.right, 5f), "An asleep member stands nowhere");

            Object.DestroyImmediate(rig.Agent.gameObject);
            roster.Step(DT);

            Assert.AreEqual(0, roster.Count);
            Assert.IsFalse(roster.AllyAhead(null, Vector3.zero, Vector3.zero, 5f), "No direction, nobody ahead");
        }

        [Test]
        public void Roster_MemberBesideTheWayNotInFront_IsNoAllyAhead()
        {
            var world = new AIWorld();
            GraphRig self = RigIn(world);
            GraphRig beside = RigIn(world, new Vector3(3f, 5f, 0f));

            Assert.IsFalse(world.Roster.AllyAhead(self.Agent, Vector3.zero, Vector3.right, 5f));

            beside.Actuator.Position = new Vector3(3f, 0.5f, 0f);
            Assert.IsTrue(world.Roster.AllyAhead(self.Agent, Vector3.zero, Vector3.right, 5f), "In the corridor");
        }

        [Test]
        public void Engagement_TargetSaysItIsNoLongerATarget_Ends()
        {
            var roster = new AgentRoster();
            Transform target = NewTarget(Vector3.zero);
            Perceivable perceivable = target.gameObject.AddComponent<Perceivable>();
            roster.EngagementOf(target);

            perceivable.Alive = false;
            roster.Step(DT);

            Assert.IsEmpty(roster.Engagements);
        }

        [Test]
        public void Engagement_TargetWithoutASay_AThirdOfTheRosterAttacks()
        {
            var world = new AIWorld();
            for (int i = 0; i < 4; i++)
                RigIn(world);
            Engagement engagement = world.Roster.EngagementOf(NewTarget(Vector3.zero));

            Assert.AreEqual(RoleAssignment.AttackersFor(4), engagement.CapacityOf(POOL));
            Assert.AreEqual(2, engagement.CapacityOf(POOL));
        }

        // ---------- abilities ----------

        [Test]
        public void Abilities_AskedAboutWhatIsNotThere_No()
        {
            var abilities = new AgentAbilities();
            abilities.Add(new AbilityDefinition { Id = "Swing" });

            Assert.IsFalse(abilities.Has(""));
            Assert.IsFalse(abilities.IsReady("Kick", null));
        }

        [Test]
        public void Abilities_NeedsATarget_NoTarget_NotReady()
        {
            var abilities = new AgentAbilities();
            abilities.Add(new AbilityDefinition { Id = "Swing", RequiresTarget = true });

            Assert.IsFalse(abilities.IsReady("Swing", new AgentContext(null, null, 1)));
        }

        [Test]
        public void Abilities_StillPlaying_NotReadyAgain()
        {
            var rig = new GraphRig(trace: false);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.AddAbility("Swing", duration: 0.5f, cooldown: 0f);
            Assert.IsTrue(rig.Abilities.TryUse("Swing", rig.Context));

            Assert.IsFalse(rig.Abilities.IsReady("Swing", rig.Context));
            rig.DestroyTarget();
        }

        [Test]
        public void Abilities_NeedsGround_InTheAir_NotReady()
        {
            var rig = new GraphRig(trace: false);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.Abilities.Add(new AbilityDefinition { Id = "Stomp", RequiresGround = true });
            Assume.That(rig.Abilities.IsReady("Stomp", rig.Context));

            rig.Actuator.IsGrounded = false;

            Assert.IsFalse(rig.Abilities.IsReady("Stomp", rig.Context));
            rig.DestroyTarget();
        }

        [Test]
        public void Abilities_NoStrategy_TheDefaultChooses()
        {
            var abilities = new AgentAbilities { Strategy = null };
            abilities.Add(new AbilityDefinition { Id = "Swing", RequiresTarget = false, RequiresGround = false });

            Assert.AreEqual("Swing", abilities.Select(new AgentContext(null, null, 1)));
        }

        [Test]
        public void TimedRunner_Cancelled_StopsAtOnce()
        {
            var runner = new TimedAbilityRunner(new AbilityDefinition { Id = "Swing", WindUp = 0.2f, Recovery = 0.2f });
            Assert.AreEqual(AbilityPhase.None, runner.Phase, "Idle: no phase");

            runner.Begin(null);
            Assert.AreEqual(AbilityPhase.WindUp, runner.Phase);
            runner.Cancel();

            Assert.IsFalse(runner.IsRunning);
            Assert.AreEqual(AbilityPhase.None, runner.Phase);
        }
    }
}
