using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The reactions to what a target does (step 90, C8; handbook §19.1–19.4, §20б.4 p.5, §38 №15, №35–37): decisions of
    /// the Brain taken out of turn. An event is caught on its rising edge, the chance rolled once for it with the agent's
    /// seeded random, the reaction waits its reaction time and looks at the event again; taken, the reaction holds until
    /// its chain ends, then lets go until a new event and its cooldown — never in the middle of the agent's own blow.
    /// Then each reaction's own event: Target Attacks, Target Opens Up, Got Hit, Health Low (mine, the target's, an
    /// ally's), and the graph problem of a reaction below the fight it must cut in on.
    /// </summary>
    public sealed class ReactionTests
    {
        private const string TARGET = "Target";
        private const float STEP = 0.05f;
        private const float NEAR = 2f;

        private GraphRig _rig;
        private GameObject _target;
        private FakeStatus _status;
        private BrainGraphBuilder _graph;
        private AgentState _brain;
        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp()
        {
            _rig = NewRig(7);
            _target = new GameObject("Player") { hideFlags = HideFlags.HideAndDontSave };
            _target.transform.position = new Vector3(NEAR, 0f, 0f);       // in front: the test body faces +X
            _status = _target.AddComponent<FakeStatus>();
            NewGraph();
            _entered.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            GraphRig.DestroyAll();
        }

        // ---------- the lifecycle of a reaction ----------

        [Test]
        public void TargetAttacks_TheTargetWindsUpInFront_IsTakenAfterTheReactionTime_NotBefore()
        {
            const float reactionTime = 0.2f;
            Decide(new TargetAttacksReaction { Chance = 1f, ReactionTime = reactionTime, ReactionTimeMax = reactionTime }, 1,
                Hold("Dodge", 0.3f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            float started = _rig.Context.LocalTime;

            Assert.IsTrue(Reaches("Dodge", 1f), "The target winds up in front, within reach: the reaction is taken");
            Assert.That(TimeOf("Dodge") - started, Is.InRange(reactionTime - 1e-4f, reactionTime + 2 * STEP + 1e-4f),
                "Not frame-perfect (handbook §38 №35): the reaction time first, then at the next think");
        }

        [Test]
        public void Reaction_TheEventHolds_ItIsAnsweredOnce_OnlyANewRiseIsAnsweredAgain()
        {
            Decide(Attacks(), 1, Hold("Dodge", 0.3f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            _rig.StepSeconds(3f);

            Assert.AreEqual(1, Count("Dodge"), "A wind-up held for 3 s is one event (handbook §38 №15: the edge, not the truth)");
            Assert.AreEqual("Rest", _rig.StateName, "Its chain ended: the reaction let go");
            Assert.AreEqual("done — waits for the next one", WhyNot("Target Attacks"));

            _status.WindingUp = false;
            _rig.Step(STEP, 2);
            _status.WindingUp = true;

            Assert.IsTrue(Reaches("Dodge", 1f), "A new wind-up is a new event");
            Assert.AreEqual(2, Count("Dodge"));
        }

        [Test]
        public void Reaction_TheChanceIsRolledOncePerEvent_NotEveryThink_AndTheSameSeedGivesTheSameAnswers()
        {
            const int events = 30;
            List<int> first = AnsweredEvents(11, events);
            List<int> again = AnsweredEvents(11, events);

            Assert.That(first.Count, Is.InRange(5, events - 5),
                "Chance 0.5 rolled once per event: about half answered — rolled every think, nearly all of them would be");
            CollectionAssert.AreEqual(first, again, "The agent's seed decides: the same seed, the same answers");
        }

        [Test]
        public void Reaction_TheAttackIsCancelledDuringTheReactionTime_NoReaction()
        {
            const float reactionTime = 0.3f;
            var reaction = new TargetAttacksReaction
            {
                Chance = 1f, ReactionTime = reactionTime, ReactionTimeMax = reactionTime, Cooldown = 0f
            };
            Decide(reaction, 1, Hold("Dodge", 0.3f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            _rig.Step(STEP, 2);
            _status.WindingUp = false;                  // a feint: over before the reaction time is
            _rig.StepSeconds(1f);

            Assert.AreEqual(0, Count("Dodge"), "Looked at again when the time was up (§38 №36): no block into the void");
            Assert.AreEqual(ReactionPhase.Idle, reaction.PhaseFor(_rig.Context));

            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge", 1f), "A wind-up that lasts is answered");
        }

        [Test]
        public void Reaction_Cooldown_AnEventSoonAfterIsLetPass_OneAfterItIsAnswered()
        {
            const float cooldown = 1f;
            Decide(new TargetAttacksReaction { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = cooldown }, 1,
                Hold("Dodge", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge"));
            Assert.IsTrue(Reaches("Rest"));
            _status.WindingUp = false;
            _rig.Step(STEP, 2);
            _status.WindingUp = true;                   // a second attack at once
            _rig.StepSeconds(0.3f);

            Assert.AreEqual(1, Count("Dodge"), "In the cooldown the next event is let pass");
            StringAssert.StartsWith("cooldown", WhyNot("Target Attacks"));

            _status.WindingUp = false;
            _rig.StepSeconds(cooldown);
            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge", 1f), "After the cooldown the next event is answered");
            Assert.AreEqual(2, Count("Dodge"));
        }

        [Test]
        public void Reaction_IsTakenOutOfTurn_WithoutWaitingTheMinimumTimeOfTheDecisionJustTaken()
        {
            Decide(Attacks(), 1, Hold("Dodge", 0.3f));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));
            float chaseAt = TimeOf("Chase Block");

            _status.WindingUp = true;

            Assert.IsTrue(Reaches("Dodge", 1f));
            Assert.Less(TimeOf("Dodge") - chaseAt, BehaviorGraphInstance.MIN_DECISION_TIME,
                "A reaction does not wait out the moment a decision just taken is kept for");
        }

        [Test]
        public void Reaction_NeverInTheMiddleOfTheAgentsOwnBlow_TakenRightAfterIt()
        {
            const float windUp = 0.5f;
            const float recovery = 0.4f;
            HitProbe hits = _target.AddComponent<HitProbe>();
            _target.transform.position = new Vector3(1f, 0f, 0f);
            _rig.GiveWeapon(reach: 2f, windUp: windUp, recovery: recovery);
            Decide(Attacks(), 1, Hold("Dodge", 0.3f));
            Decide(Fight(), 2, Block("Strike", new AttackTask { Move = "Normal" }));
            Run();
            Assert.IsTrue(Reaches("Strike"));
            float blowAt = TimeOf("Strike");
            _rig.Step(STEP, 2);

            _status.WindingUp = true;                   // the target winds up while the agent's blow is on its way

            Assert.IsTrue(Reaches("Dodge", 2f));
            Assert.GreaterOrEqual(TimeOf("Dodge") - blowAt, windUp + recovery - 1e-4f,
                "A blow that has started is played out (handbook §18.2 p.3, §20б.4 p.1)");
            Assert.AreEqual(1, hits.Hits, "The blow landed before the reaction");
        }

        [Test]
        public void Reaction_AWindUpThatStartsAndEndsDuringTheAgentsBlow_IsSeen_ButNotAnsweredAfterIt()
        {
            _target.transform.position = new Vector3(1f, 0f, 0f);
            _rig.GiveWeapon(reach: 2f, windUp: 0.5f, recovery: 0.4f);
            TargetAttacksReaction reaction = Attacks();
            Decide(reaction, 1, Hold("Dodge", 0.3f));
            Decide(Fight(), 2, Block("Strike", new AttackTask { Move = "Normal" }));
            Run();
            Assert.IsTrue(Reaches("Strike"));
            _rig.Step(STEP, 2);

            var phases = new List<ReactionPhase>();
            _status.WindingUp = true;
            for (int i = 0; i < 4; i++)
            {
                _rig.Step(STEP);
                phases.Add(reaction.PhaseFor(_rig.Context));
            }

            _status.WindingUp = false;                  // over while the agent's blow still goes on
            _rig.StepSeconds(1.5f);

            CollectionAssert.Contains(phases, ReactionPhase.Ready,
                "Observe looks every think, also in the middle of a blow: the wind-up was seen");
            Assert.AreEqual(0, Count("Dodge"), "Over by the time the blow ended: nothing to answer any more");
            Assert.AreEqual(ReactionPhase.Idle, reaction.PhaseFor(_rig.Context));
        }

        [Test]
        public void Reaction_HoldsUntilItsChainEnds_EvenWhenTheEventIsOverMeanwhile()
        {
            const float dodge = 0.6f;
            Decide(Attacks(), 1, Hold("Dodge", dodge));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge"));
            float dodgeAt = TimeOf("Dodge");
            _rig.Step(STEP, 2);
            _status.WindingUp = false;                  // the attack is over, the dodge goes on

            Assert.IsTrue(Reaches("Rest", 2f));
            Assert.GreaterOrEqual(TimeOf("Rest") - dodgeAt, dodge - 1e-4f, "The chain runs to its end");
            Assert.AreEqual(1, Count("Dodge"), "…once");
        }

        [Test]
        public void TargetAttacks_RecentHitsOnTheAgent_RaiseTheChance()
        {
            Decide(new TargetAttacksReaction
            {
                Chance = 0f, PerRecentHit = 0.6f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f
            }, 1, Hold("Dodge", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            for (int i = 0; i < 5; i++)
                Feint(0.3f, 0.1f);
            Assert.AreEqual(0, Count("Dodge"), "Chance 0 and no blows lately: never");

            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            _status.WindingUp = true;

            Assert.IsTrue(Reaches("Dodge", 1f), "Two blows just now: 0 + 0.6 × 2 — the guard goes up (handbook §19.2)");
        }

        // ---------- the events of the reactions ----------

        [Test]
        public void TargetAttacks_NotWindingUp_TooFar_Behind_NoStatus_IsNoEvent_AndSaysWhy()
        {
            var reaction = new TargetAttacksReaction { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Range = 3f };
            Decide(reaction, 1, Hold("Dodge", 0.3f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            Assert.AreEqual("target not winding up", After(0.2f));

            _status.WindingUp = true;
            _target.transform.position = new Vector3(4f, 0f, 0f);
            Assert.AreEqual("target 4.0 m, needs 3.0 or closer", After(0.2f));

            _target.transform.position = new Vector3(-NEAR, 0f, 0f);
            Assert.AreEqual("target not in front", After(0.2f), "Only from the front (handbook §19.2: dot 0.5)");

            _target.transform.position = new Vector3(NEAR, 0f, 0f);
            _status.Says = false;
            Assert.AreEqual("Player does not say whether it winds up", After(0.2f), "No data is no event, not \"no\"");

            Object.DestroyImmediate(_status);
            Assert.AreEqual("no status on Player", After(0.2f));
            Assert.AreEqual(0, Count("Dodge"));
        }

        [Test]
        public void TargetOpensUp_RecoveringOrStaggeredWithinRange_IsAnEvent_AWindUpIsNot()
        {
            Decide(new TargetOpensUpReaction { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f }, 1,
                Hold("Punish", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;
            Assert.AreEqual("target not open", After(0.3f));

            _status.WindingUp = false;
            _status.Recovering = true;
            Assert.IsTrue(Reaches("Punish", 1f), "Recovering after its blow: open (handbook §19.1)");
            Assert.IsTrue(Reaches("Rest"));
            _status.Recovering = false;
            _rig.Step(STEP, 2);

            _status.Staggered = true;
            Assert.IsTrue(Reaches("Punish", 1f), "Staggered: open");
            Assert.AreEqual(2, Count("Punish"));
        }

        [Test]
        public void GotHit_EachNewHitIsAnEvent_WithinTheWindow_FromBehindOnlyLeavesTheFrontAlone()
        {
            var reaction = new GotHitReaction
            {
                Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f, Window = 1f
            };
            Decide(reaction, 1, Hold("Turn", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));
            Assert.AreEqual("not hit", WhyNot("Got Hit"));

            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            Assert.IsTrue(Reaches("Turn", 1f));
            Assert.IsTrue(Reaches("Rest"));
            _rig.StepSeconds(0.3f);
            Assert.AreEqual(1, Count("Turn"), "One hit, one answer — the window still open does not answer it again");

            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            Assert.IsTrue(Reaches("Turn", 1f), "A new hit is a new event");

            Assert.IsTrue(Reaches("Rest"));
            reaction.FromBehindOnly = true;
            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));       // the attacker stands in front
            Assert.AreEqual("hit from the front", After(0.3f));
            Assert.AreEqual(2, Count("Turn"));

            _target.transform.position = new Vector3(-NEAR, 0f, 0f);
            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            Assert.IsTrue(Reaches("Turn", 1f), "From behind: answered");
        }

        [Test]
        public void GotHit_NoTargetSeenYet_WaitsForOneWithinTheWindow()
        {
            Decide(new GotHitReaction { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Window = 1f }, 1,
                Hold("Turn", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);         // struck by somebody nobody has seen
            Assert.IsTrue(Reaches("Rest"));

            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            Assert.AreEqual("no target", After(0.3f));

            _rig.Agent.Blackboard.Set(TARGET, _target.transform);       // Detection's Hit By sees the attacker
            Assert.IsTrue(Reaches("Turn", 1f), "Seen within the window: the hit is answered");
        }

        [Test]
        public void Reaction_TheGraphStopsInItsChain_ItIsLetGo_AndAnswersTheNextEventAfterTheRestart()
        {
            Decide(Attacks(), 1, Hold("Dodge", 5f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));
            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge"));

            _rig.Instance.Stop(_rig.Context);           // switched off in the middle of its chain (a cutscene, death)
            _status.WindingUp = false;
            _rig.StepSeconds(0.3f);                     // started anew, in the Brain's hands
            Assert.AreEqual("Rest", _rig.StateName);

            _status.WindingUp = true;
            Assert.IsTrue(Reaches("Dodge", 1f), "Not stuck in the chain it was taken out of");
            Assert.AreEqual(2, Count("Dodge"));
        }

        [Test]
        public void HealthLow_Mine_UnderTheLine_HoldsTillAboveTheLineWithSlack_ThenAgainOnlyWhenItFallsAgain()
        {
            FakeHealth health = Health(_rig.Agent.gameObject, 50f);
            Decide(new HealthLowReaction
            {
                Whose = HealthSubject.Me, Below = 30f, Slack = 10f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f
            }, 1, Hold("Flee"));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));
            Assert.AreEqual("health 50%, low below 30%", WhyNot("Health Low"));

            health.CurrentHealth = 25f;
            Assert.IsTrue(Reaches("Flee", 1f), "Under the line");

            health.CurrentHealth = 35f;
            _rig.StepSeconds(0.5f);
            Assert.AreEqual("Flee", _rig.StateName, "Above the line, but within its slack: still low");

            health.CurrentHealth = 45f;
            Assert.IsTrue(Reaches("Rest", 1f), "Above the line with its slack: the reaction lets go (handbook §17)");

            health.CurrentHealth = 25f;
            Assert.IsTrue(Reaches("Flee", 1f), "Under the line again: a new event");
        }

        [Test]
        public void HealthLow_TheTargets_UnderTheLine_IsAnEvent_NoHealthIsNot()
        {
            Decide(new HealthLowReaction
            {
                Whose = HealthSubject.Target, Below = 30f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f
            }, 1, Hold("Finish Off"));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));
            Assert.AreEqual("no health on Player", WhyNot("Health Low"));

            FakeHealth health = Health(_target, 80f);
            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            _rig.Step(STEP, 2);
            _rig.Agent.Blackboard.Set(TARGET, _target.transform);       // seen anew: its health is looked for anew
            Assert.AreEqual("health 80%, low below 30%", After(0.2f));

            health.CurrentHealth = 20f;
            Assert.IsTrue(Reaches("Finish Off", 1f));
        }

        [Test]
        public void HealthLow_AnAlly_TheLowestOfMyFactionWithinRange_NotAStranger_NotOneFarAway()
        {
            var world = new AIWorld();
            GraphRig me = NewRig(3, world);
            GraphRig ally = Member(world, new Vector3(3f, 0f, 0f), 0, 50f, out FakeHealth allyHealth);
            Member(world, new Vector3(2f, 0f, 0f), 5, 10f, out _);              // of another faction
            Member(world, new Vector3(20f, 0f, 0f), 0, 5f, out _);              // out of Range
            var reaction = new HealthLowReaction
            {
                Whose = HealthSubject.Ally, Below = 30f, Range = 10f, ReactionTime = 0f, ReactionTimeMax = 0f
            };
            Decide(reaction, 1, Hold("Help"));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run(me);
            Assert.IsTrue(Reaches("Rest", rig: me));
            Assert.AreSame(ally.Agent, reaction.LowestAllyFor(me.Context), "Of my faction, within Range: the only ally");
            Assert.AreEqual("health 50%, low below 30%", WhyNot("Health Low", me));

            allyHealth.CurrentHealth = 20f;

            Assert.IsTrue(Reaches("Help", 1f, me), "The ally is low: the agent reacts");
            Assert.IsFalse(reaction.NeedsTarget, "An ally's health needs no target of the Brain");
        }

        // ---------- the graph ----------

        [Test]
        public void Validator_AReactionLessImportantThanADecisionThatAttacks_IsAProblem_AboveItIsNot()
        {
            List<GraphIssue> below = Issues(reactionPriority: 3, fightPriority: 1);
            List<GraphIssue> above = Issues(reactionPriority: 1, fightPriority: 2);
            List<GraphIssue> belowPatrolOnly = Issues(reactionPriority: 1, fightPriority: 2, patrolPriority: 0);

            GraphIssue problem = below.Single(issue => issue.Code == GraphIssueCode.ReactionBelowDecision);
            Assert.IsFalse(problem.IsError, "A warning: the graph runs, the reaction just never cuts in on the fight");
            StringAssert.Contains($"'Target Attacks' (priority 3) is less important than '{NodeTitles.Of(typeof(FightReasoner))}' (1)",
                problem.Message);
            Assert.IsFalse(above.Exists(issue => issue.Code == GraphIssueCode.ReactionBelowDecision));
            Assert.IsFalse(belowPatrolOnly.Exists(issue => issue.Code == GraphIssueCode.ReactionBelowDecision),
                "Below a decision whose chain does not attack: nothing to cut in on");
        }

        [Test]
        public void Reaction_SaysOnItsNodeThatItIsTakenOutOfTurn()
        {
            foreach (ReactionDecision reaction in new ReactionDecision[]
                     {
                         new TargetAttacksReaction(), new TargetOpensUpReaction(), new GotHitReaction(), new HealthLowReaction()
                     })
            {
                Assert.IsTrue(reaction.OutOfTurn, NodeTitles.Of(reaction));
                StringAssert.Contains("out of turn", reaction.Requirement, "The canvas writes it under the node's settings");
            }

            StringAssert.Contains("of my faction", new HealthLowReaction { Whose = HealthSubject.Ally }.Requirement,
                "Where the allies come from: no wire shows it");
        }

        [Test]
        public void Dash_AsideOrBack_NeverGoesTowardTheTarget()
        {
            foreach (Vector3 inward in new[] { Vector3.right, Vector3.forward, new Vector3(-0.6f, 0f, 0.8f) })
            {
                Assert.Less(Vector3.Dot(DashTask.Way(DashDirection.Back, inward), inward), -0.99f, "Back: away from it");
                Assert.AreEqual(0f, Vector3.Dot(DashTask.Way(DashDirection.Left, inward), inward), 1e-5f, "Aside: across");
                Assert.AreEqual(0f, Vector3.Dot(DashTask.Way(DashDirection.Right, inward), inward), 1e-5f, "Aside: across");
            }
        }

        [Test]
        public void TargetAttacks_ADodgeThatCannotBeMade_TheChainBlocks()
        {
            _rig.GiveWeapon(reach: 2f, windUp: 0.3f, recovery: 0.3f);      // a weapon with no dash at all
            AgentState random = _graph.Block(new PickRandomTask(), "Random");
            AgentState aside = _graph.Block(new DashTask { Direction = DashDirection.Aside }, "Dash Aside");
            AgentState back = _graph.Block(new DashTask { Direction = DashDirection.Back }, "Dash Back");
            AgentState block = _graph.Block(new BlockTask { Hold = 0.5f }, "Block");
            _graph.Out(random, 0, aside).Out(random, 1, back);
            _graph.OnFail(aside, block).OnFail(back, block);
            Decide(Attacks(), 1, random);
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run();
            Assert.IsTrue(Reaches("Rest"));

            _status.WindingUp = true;

            Assert.IsTrue(Reaches("Block", 1f), "No dodge on the weapon: a block, not nothing (handbook §19.2)");
            Assert.IsTrue(_rig.Context.IsBlocking);
            Assert.AreEqual(0, _rig.Actuator.Dashes);
        }

        [Test]
        public void Reactions_AThinkWhileNobodyLooks_AllocatesNothing()
        {
            _rig.Context.ExplainsDecisions = false;
            Health(_rig.Agent.gameObject, 100f);
            Decide(Attacks(), 1, Hold("Dodge", 0.2f));
            Decide(new TargetOpensUpReaction { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f }, 2, Hold("Punish", 0.2f));
            Decide(new GotHitReaction { ReactionTime = 0f, ReactionTimeMax = 0f }, 3, Hold("Turn", 0.2f));
            Decide(new HealthLowReaction { Whose = HealthSubject.Me }, 4, Hold("Flee", 0.2f));
            Decide(new PatrolReasoner(), 5, Hold("Rest"));
            Run(track: false);

            for (int i = 0; i < 3; i++)
                Exchange();                             // every reaction taken, every list grown

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            Assert.That(Exchange, NoAllocation.Constraint,
                "The reactions look every think: while nobody looks they allocate nothing");
        }

        // ---------- helpers ----------

        private static TargetAttacksReaction Attacks() =>
            new() { Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f };

        private static FightReasoner Fight() => new() { Closer = 1.5f, LetGoAfter = 2f };

        // A blow, a wind-up, a recovery and a hit — what the reactions above look at
        private void Exchange()
        {
            _status.WindingUp = true;
            _rig.StepSeconds(0.5f);
            _status.WindingUp = false;
            _status.Recovering = true;
            _rig.StepSeconds(0.5f);
            _status.Recovering = false;
            _rig.Agent.TakeHit(new HitInfo(_target.transform, 1f));
            _rig.StepSeconds(0.5f);
        }

        // A wind-up for the time given, then a pause
        private void Feint(float on, float off)
        {
            _status.WindingUp = true;
            _rig.StepSeconds(on);
            _status.WindingUp = false;
            _rig.StepSeconds(off);
        }

        // Chance 0.5 over a number of events, each a long wind-up: which of them were answered
        private List<int> AnsweredEvents(int seed, int events)
        {
            GraphRig rig = NewRig(seed);
            var graph = new BrainGraphBuilder();
            graph.BrainTask.Target = TARGET;
            graph.Variable(TARGET, VariableType.Transform);
            AgentState reaction = graph.Decision(new TargetAttacksReaction
            {
                Chance = 0.5f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f
            }, 1);
            graph.Do(reaction, graph.Block(new WaitTask(0.1f), "Dodge"));
            graph.Do(graph.Decision(new PatrolReasoner(), 2), graph.Block(new WaitTask(100f), "Rest"));
            rig.SetGraph(graph.Build());
            rig.Agent.Blackboard.Set(TARGET, _target.transform);
            int answered = 0;
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null && to.DisplayName == "Dodge")
                    answered++;
            };

            var which = new List<int>();
            rig.StepSeconds(0.2f);
            for (int e = 0; e < events; e++)
            {
                int before = answered;
                _status.WindingUp = true;
                rig.StepSeconds(1f);                    // 20 thinks of the same wind-up
                _status.WindingUp = false;
                rig.StepSeconds(0.3f);
                if (answered > before)
                    which.Add(e);
            }

            return which;
        }

        private List<GraphIssue> Issues(int reactionPriority, int fightPriority, int patrolPriority = 5)
        {
            var graph = new BrainGraphBuilder();
            graph.BrainTask.Target = TARGET;
            graph.Variable(TARGET, VariableType.Transform);
            graph.Chain(graph.Decision(new TargetAttacksReaction(), reactionPriority), new WaitTask(0.3f));
            graph.Chain(graph.Decision(new FightReasoner(), fightPriority), new AttackTask());
            graph.Chain(graph.Decision(new PatrolReasoner(), patrolPriority), new WaitTask(1f));
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(graph.Build(), asset);
                return GraphValidator.Validate(asset);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static GraphRig NewRig(int seed, IAIWorld world = null)
        {
            var rig = new GraphRig(seed, trace: false, world: world) { ThinkInterval = 0f };
            rig.Context.ExplainsDecisions = true;       // as the graph window does for the agent it shows
            Transform self = rig.Context.Self;
            if (self != null)
                self.rotation = Quaternion.LookRotation(rig.Actuator.Facing);    // where the test body faces: +X
            return rig;
        }

        // An agent of the world at a place, of a faction, with a health in percent
        private static GraphRig Member(IAIWorld world, Vector3 at, int faction, float health, out FakeHealth fake)
        {
            GraphRig rig = NewRig(1, world);
            rig.Actuator.Position = at;
            rig.Agent.Faction = faction;
            rig.Agent.gameObject.SetActive(true);       // a member of the world in play (Edit Mode sends it no messages)
            fake = Health(rig.Agent.gameObject, health);
            return rig;
        }

        private static FakeHealth Health(GameObject owner, float percent)
        {
            owner.SetActive(true);                      // its components are found (Edit Mode sends them no messages)
            FakeHealth health = owner.AddComponent<FakeHealth>();
            health.MaxHealth = 100f;
            health.CurrentHealth = percent;
            return health;
        }

        private void NewGraph()
        {
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _brain = _graph.Brain;
        }

        private AgentState Decide(ReasonerTask decision, int priority, AgentState chain)
        {
            AgentState state = _graph.Decision(decision, priority);
            _graph.Do(state, chain);
            return state;
        }

        private AgentState Block(string name, AgentTask task) => _graph.Block(task, name);

        private AgentState Hold(string name, float seconds = 100f) => Block(name, new WaitTask(seconds));

        private void Run(GraphRig rig = null, bool track = true)
        {
            rig ??= _rig;
            rig.SetGraph(_graph.Build());
            rig.Agent.Blackboard.Set(TARGET, _target.transform);
            if (!track)
                return;

            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, rig.Context.LocalTime));
            };
        }

        private bool Reaches(string state, float seconds = 2f, GraphRig rig = null)
        {
            rig ??= _rig;
            return rig.StepUntil(() => rig.StateName == state, seconds, STEP);
        }

        // The reaction's verdict after a while of thinking
        private string After(float seconds)
        {
            _rig.StepSeconds(seconds);
            return CombatBrainTask.VerdictsOf(_brain, _rig.Context)
                .First(verdict => verdict.State.Reasoner is ReactionDecision).WhyNot;
        }

        private string WhyNot(string decision, GraphRig rig = null)
        {
            rig ??= _rig;
            return CombatBrainTask.VerdictsOf(_brain, rig.Context).Single(verdict => verdict.State.DisplayName == decision)
                .WhyNot;
        }

        private int Count(string state) => _entered.Count(entry => entry.Name == state);

        private float TimeOf(string state) => _entered.Last(entry => entry.Name == state).Time;
    }
}
