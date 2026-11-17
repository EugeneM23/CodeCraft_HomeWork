using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Brain and the decisions wired to it, on a real agent: the most important decision that holds is taken and its
    /// chain of blocks runs; a decision just taken is kept a moment (no flicker), one whose chain has ended is not; "not
    /// from" lists, equal priorities, a blow that has started, the Brain's target for the blocks of a chain, and what
    /// the canvas shows in Play — every decision's verdict.
    /// </summary>
    public sealed class BrainDecisionTests
    {
        private const string TARGET = "Target";
        private const string DUMMY = "Dummy";
        private const float STEP = 0.1f;
        private const float NEAR = 1f;
        private const float FAR = 5f;

        private GraphRig _rig;
        private GameObject _victim;
        private HitProbe _hits;
        private BrainGraphBuilder _graph;
        private AgentState _brain;
        private readonly List<(string Name, float Time)> _entered = new();
        private readonly Dictionary<string, string> _whyNotWhenEntered = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            _rig.Context.ExplainsDecisions = true;      // as the graph window does for the agent it shows
            _victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            _victim.transform.position = new Vector3(FAR, 0f, 0f);
            _hits = _victim.AddComponent<HitProbe>();
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _graph.Variable(DUMMY, VariableType.Transform);
            _brain = _graph.Brain;
            _entered.Clear();
            _whyNotWhenEntered.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_victim);
            GraphRig.DestroyAll();
        }

        [Test]
        public void Brain_TakesTheMostImportantDecisionThatHolds_AndRunsItsChain()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            Run();

            Assert.IsTrue(Reaches("Chase Block"), "Fight does not hold at 5 m; Chase is more important than Patrol");
            CollectionAssert.AreEqual(new[] { "Chase", "Chase Block" }, Names().Where(name => name != "Brain").ToList(),
                "The decision, then the first block of its chain — and nothing else on the way");
        }

        [Test]
        public void Brain_AMoreImportantDecisionStartsToHold_IsTakenOnlyAfterTheMinimumTime()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));

            _victim.transform.position = new Vector3(NEAR, 0f, 0f);

            Assert.IsTrue(Reaches("Fight Block"), "The more important decision wins");
            Assert.GreaterOrEqual(TimeOf("Attack") - TimeOf("Chase"), BehaviorGraphInstance.MIN_DECISION_TIME - 1e-4f,
                "A decision just taken is kept for a moment: no flicker");
        }

        [Test]
        public void Brain_NoTargetSeen_TakesTheDecisionThatNeedsNone()
        {
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run(withTarget: false);

            Assert.IsTrue(Reaches("Patrol Block"));
        }

        [Test]
        public void Brain_NoTargetWiredIn_TakesPatrol_ButNeverFight()
        {
            _graph.BrainTask.Target = "";
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);         // in reach — and in the variable, unwired
            Run();

            Assert.IsTrue(Reaches("Patrol Block"), "A decision that needs no target is taken");
            _rig.StepSeconds(1f);

            Assert.AreEqual("Patrol Block", _rig.StateName, "Nothing wired into Target: the fight is never taken");
            Assert.AreEqual("no target", WhyNot("Attack"), "…the Brain has nothing to think about");
            Assert.IsNull(_rig.Context.BrainTarget);
        }

        [Test]
        public void Watch_HoldsWhileTheBrainSeesItsTarget()
        {
            Decide(new WatchReasoner(), 1, Hold("Watch Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run();

            Assert.IsTrue(Reaches("Watch Block"), "The target seen: Watch");

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.IsTrue(Reaches("Patrol Block"), "The target gone: Watch lets go");
            Assert.AreEqual("no target", WhyNot("Watch"));

            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            Assert.IsTrue(Reaches("Watch Block"), "Seen again: Watch again");
        }

        [Test]
        public void Brain_NothingHolds_StandsInTheBrain_EveryVerdictSaysWhy()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new UseAbilityReasoner(), 2, Hold("Grenade Block"));
            Run();

            _rig.Step(STEP, 3);

            Assert.AreEqual("Brain", _rig.StateName, "No decision holds: the agent waits in the Brain");
            IReadOnlyList<ReasonerVerdict> verdicts = CombatBrainTask.VerdictsOf(_brain, _rig.Context);
            Assert.AreEqual(2, verdicts.Count, "One verdict per decision wired to the Brain");
            Assert.IsFalse(verdicts.Any(verdict => verdict.Won || verdict.Holds));
            CollectionAssert.AreEquivalent(new[] { "target 5.0 m, needs 1.5 or closer", "no ability chosen" },
                verdicts.Select(verdict => verdict.WhyNot));
        }

        [Test]
        public void Brain_Verdicts_TheWinnerWon_TheOthersHoldOrSayWhy()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));
            _rig.Step(STEP, 1);

            Dictionary<string, ReasonerVerdict> verdicts =
                CombatBrainTask.VerdictsOf(_brain, _rig.Context).ToDictionary(verdict => verdict.State.DisplayName);
            Assert.IsTrue(verdicts["Chase"].Won);
            Assert.IsFalse(verdicts["Attack"].Holds);
            Assert.IsTrue(verdicts["Patrol"].Holds, "Holds — a more important decision won");
            Assert.IsFalse(verdicts["Patrol"].Won);
        }

        [Test]
        public void Brain_NotFrom_TheBlockedDecisionIsNotTakenFromThatOne_ButFromTheNext()
        {
            AgentState fight = Decide(Fight(), 1, Hold("Fight Block"));
            var chase = new ChaseReasoner();
            Decide(chase, 2, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();
            chase.NotFrom.Add(fight.Id);
            Assert.IsTrue(Reaches("Fight Block"));
            _rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME);
            _entered.Clear();

            _victim.transform.position = new Vector3(FAR, 0f, 0f);

            Assert.IsTrue(Reaches("Chase Block"), "From Patrol it may be taken");
            List<string> path = Names();
            Assert.Less(path.IndexOf("Patrol"), path.IndexOf("Chase"), "Chase is not taken from Fight: the next that holds is");
            Assert.AreEqual("not from Attack", _whyNotWhenEntered["Patrol/Chase"]);
        }

        [Test]
        public void Brain_EqualPriorities_TheOlderIsTaken_ButTheCurrentOneKeeps()
        {
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 1, Hold("Patrol Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"), "Both hold: the one made earlier wins");

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.IsTrue(Reaches("Patrol Block"));
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            _rig.StepSeconds(1f);

            Assert.AreEqual("Patrol Block", _rig.StateName, "Chase holds again, as important — the current decision keeps");
        }

        [Test]
        public void Brain_AChainEnds_TheBrainChoosesAgainAtOnce_AndTheSameDecisionRunsItsChainAnew()
        {
            Decide(new ChaseReasoner(), 1, Short("Chase Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run();

            _rig.StepSeconds(2f);

            Assert.GreaterOrEqual(Names().Count(name => name == "Chase Block"), 3, "Its chain ran again and again");
            CollectionAssert.DoesNotContain(Names(), "Patrol Block", "Never the less important one");
        }

        [Test]
        public void Brain_AChainThatEndsAtOnce_DoesNotHoldItsDecisionAgainstAMoreImportantOne()
        {
            // Step 70: re-taking the same decision after its chain ended does not start its minimum time anew
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Short("Chase Block", 0f));
            Run();
            _rig.StepSeconds(1f);

            _victim.transform.position = new Vector3(NEAR, 0f, 0f);

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Fight Block", BehaviorGraphInstance.MIN_DECISION_TIME, STEP),
                "The chase had nothing running to keep: the fight is taken at once");
        }

        [Test]
        public void Brain_ABlowHasStarted_TheMoreImportantDecisionWaitsForItsEnd()
        {
            const float windUp = 0.5f;
            const float recovery = 0.4f;
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 2,
                Block("Strike", Strike(DUMMY, 100f, windUp, recovery)));
            Run(withTarget: false);
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Assert.IsTrue(Reaches("Strike"));
            _rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME);

            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);     // Chase holds now, and is more important
            _rig.Step(STEP, 1);

            Assert.AreEqual("Strike", _rig.StateName, "A blow that has started is not cut short");
            Assert.IsTrue(Reaches("Chase Block", windUp + recovery + 3 * STEP));
            Assert.AreEqual(1, _hits.Hits, "The blow landed before the switch");
        }

        [Test]
        public void Brain_ABlockWithNoTargetOfItsOwn_WorksWithTheBrainsTarget()
        {
            Decide(Fight(), 1, Block("Strike", Strike("", 2f, 0.1f, 0.1f, 2f)));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits > 0, 2f, STEP), "Strike with an empty Target hits the Brain's target");
            Assert.AreEqual(2f, _hits.LastDamage);
            Assert.AreSame(_victim.transform, _rig.Context.BrainTarget);
        }

        [Test]
        public void Brain_TheTargetIsGone_TheBrainsTargetIsGoneToo()
        {
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));
            Assert.AreSame(_victim.transform, _rig.Context.BrainTarget);

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            _rig.Step(STEP, 1);

            Assert.IsNull(_rig.Context.BrainTarget);
        }

        [Test]
        public void Brain_WithDecisions_WritesNoFactsOnTheBlackboard()
        {
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Run();

            int before = BlackboardSize();

            Assert.IsTrue(Reaches("Chase Block"));
            _rig.StepSeconds(1f);

            Assert.AreEqual(before, BlackboardSize(), "Nothing hidden appears on the blackboard: the facts stay in the Brain");
        }

        [Test]
        public void Brain_NobodyLooks_TheVerdictsSayNoReasonsWithNumbers()
        {
            _rig.Context.ExplainsDecisions = false;
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();

            Assert.IsTrue(Reaches("Chase Block"));

            ReasonerVerdict fight = CombatBrainTask.VerdictsOf(_brain, _rig.Context).Single(verdict => verdict.State.DisplayName == "Attack");
            Assert.IsFalse(fight.Holds);
            Assert.IsNull(fight.WhyNot, "A reason with numbers is a new string: written only while somebody looks");
        }

        [Test]
        public void Brain_AMoreImportantDecisionWaitsTheMomentOut_ItsVerdictSaysSo()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));         // the chain runs: the Brain looks again every think

            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            _rig.Step(STEP, 1);

            ReasonerVerdict fight = CombatBrainTask.VerdictsOf(_brain, _rig.Context).Single(verdict => verdict.State.DisplayName == "Attack");
            Assert.IsTrue(fight.Holds);
            Assert.IsFalse(fight.Won);
            Assert.AreEqual("holds — waits: Chase was taken a moment ago", fight.WhyNot);
        }

        [Test]
        public void Brain_AChainThatDidNothing_ItsDecisionRests_AndAnotherIsTaken()
        {
            // The dead band of a Fight whose blow cannot reach: the chain ends the moment it starts
            Decide(new FightReasoner { Closer = 10f, LetGoAfter = 10f }, 1,
                Block("Strike", Strike(TARGET, 0.5f, 0.2f, 0.2f)));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();

            Assert.IsTrue(Reaches("Chase Block"), "The Fight did nothing: it rests, the next decision is taken");
            Assert.AreEqual("did nothing a moment ago — rests",
                CombatBrainTask.VerdictsOf(_brain, _rig.Context).Single(verdict => verdict.State.DisplayName == "Attack").WhyNot);
            Assert.LessOrEqual(Names().Count(name => name == "Strike"), 1, "Not taken again think after think");
            Assert.AreEqual(0, _hits.Hits);
        }

        [Test]
        public void Brain_AChainWhoseWorkWasABlow_DidSomething_TheFightIsTakenAgainNotRested()
        {
            // The lab's skeleton graph: a blow, then a count, and the chain ends — the Brain chooses again after every
            // blow. A blow is committed (nothing cuts it short) the whole time it runs; it is still the chain's work
            const float windUp = 0.3f;
            const float recovery = 0.3f;
            AgentState strike = Block("Strike", Strike(TARGET, 2f, windUp, recovery));
            AgentState count = Block("Add", new AddNumberTask("Blows"));
            _graph.Then(strike, count);
            Decide(Fight(), 1, strike);
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits >= 4, 4f * (windUp + recovery) + 1f, STEP),
                "Blow after blow: the target stays within reach");
            CollectionAssert.DoesNotContain(Names(), "Chase Block", "The fight that struck did not rest: never the chase");
            CollectionAssert.DoesNotContain(_whyNotWhenEntered.Values, "did nothing a moment ago — rests");
        }

        [Test]
        public void Notice_ATargetJustSeen_IsNoticedFirst_ThenTheFight_AndNotNoticedAgainWhileItStaysInSight()
        {
            // Handbook §9.1, B.2: Patrol → Notice → Chase — the agent takes a moment to react (and to draw its sword)
            const float react = 0.5f;
            Decide(new NoticeReasoner { React = react }, 1, Short("Notice Block", react));
            Decide(Fight(), 2, Hold("Fight Block"));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();

            Assert.IsTrue(Reaches("Notice Block"), "A target just seen: noticed first");
            Assert.IsTrue(Reaches("Fight Block", react + 1f), "Then the fight");
            _rig.StepSeconds(2f);

            Assert.AreEqual(1, Names().Count(name => name == "Notice Block"), "Noticed once while it stays in sight");
            Assert.AreEqual("Fight Block", _rig.StateName);
        }

        [Test]
        public void Notice_TheTargetGoneAndSeenAgain_IsNoticedAgain()
        {
            const float react = 0.3f;
            Decide(new NoticeReasoner { React = react }, 1, Short("Notice Block", react));
            Decide(Fight(), 2, Hold("Fight Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();
            Assert.IsTrue(Reaches("Fight Block", 2f));

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.IsTrue(Reaches("Patrol Block"));
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);

            Assert.IsTrue(Reaches("Notice Block"), "Seen anew: noticed anew");
            Assert.AreEqual(2, Names().Count(name => name == "Notice Block"));
        }

        // ---------- weapons: Change Weapon and Kite ----------

        private (WeaponAsset Sword, WeaponAsset Bow) TwoWeapons()
        {
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            sword.name = "Sword";
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            bow.Shoots = true;
            _rig.GiveWeapon(sword);
            _rig.Carry(sword);
            _rig.Carry(bow);
            return (sword, bow);
        }

        [Test]
        public void ChangeWeapon_InAFight_IsTakenOnceItsTimeHasPassed_NotSooner()
        {
            const float every = 2f;
            TwoWeapons();
            Decide(new ChangeWeaponReasoner { Every = every, EveryMax = every }, 1,
                Short("Change Block"));
            Decide(Fight(), 2, Hold("Fight Block"));
            _victim.transform.position = new Vector3(1f, 0f, 0f);
            Run();
            Assert.IsTrue(Reaches("Fight Block"));
            float start = _rig.Context.LocalTime;

            Assert.IsFalse(Reaches("Change Block", every - 0.3f), "not before its time");
            Assert.IsTrue(Reaches("Change Block", 1f), "then the change");
            Assert.That(TimeOf("Change Block") - start, Is.EqualTo(every).Within(0.3f));
        }

        [Test]
        public void ChangeWeapon_ItsTimeCountsOnlyWithATarget_AndStartsAnewWhenTaken()
        {
            const float every = 1.5f;
            TwoWeapons();
            Decide(new ChangeWeaponReasoner { Every = every, EveryMax = every }, 1,
                Short("Change Block"));
            Decide(Fight(), 2, Hold("Fight Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            Run(withTarget: false);

            Assert.IsFalse(Reaches("Change Block", every * 2f), "no target: no time counted");

            _victim.transform.position = new Vector3(1f, 0f, 0f);
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            Assert.IsTrue(Reaches("Change Block", every + 0.5f), "with a target it counts");
            Assert.IsTrue(Reaches("Fight Block"));
            Assert.IsFalse(Reaches("Change Block", every - 0.3f), "taken: its time starts anew");
        }

        [Test]
        public void ChangeWeapon_NoTarget_TheSecondWeaponInTheHand_BackToTheFirstAtOnce()
        {
            (_, WeaponAsset bow) = TwoWeapons();
            _rig.Agent.Weapon = bow;
            Decide(new ChangeWeaponReasoner { Every = 100f, EveryMax = 100f }, 1,
                Hold("Change Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run(withTarget: false);

            Assert.IsTrue(Reaches("Change Block", 0.5f), "the bow goes after the fight");
        }

        [Test]
        public void ChangeWeapon_NoTarget_TheFirstWeaponInTheHand_NotTaken_AndSaysWhy()
        {
            TwoWeapons();
            Decide(new ChangeWeaponReasoner { Every = 100f, EveryMax = 100f }, 1,
                Hold("Change Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run(withTarget: false);

            Assert.IsTrue(Reaches("Patrol Block"));
            Assert.AreEqual("no target", CombatBrainTask.VerdictsOf(_brain, _rig.Context)
                .Single(verdict => verdict.State.DisplayName == "Switch Weapon").WhyNot);
        }

        [Test]
        public void Kite_IsTakenOnlyWithAWeaponThatShoots()
        {
            (WeaponAsset sword, WeaponAsset bow) = TwoWeapons();
            Decide(new KiteReasoner(), 1, Hold("Kite Block"));
            Decide(Fight(), 2, Hold("Fight Block"));
            _victim.transform.position = new Vector3(1f, 0f, 0f);
            Run();
            Assert.IsTrue(Reaches("Fight Block"), "a sword in the hand: no kiting");
            Assert.AreEqual("my weapon does not shoot", CombatBrainTask.VerdictsOf(_brain, _rig.Context)
                .Single(verdict => verdict.State.DisplayName == "Kite").WhyNot);

            _rig.Agent.Weapon = bow;
            Assert.IsTrue(Reaches("Kite Block"), "a bow in the hand: the same graph kites");
            Assert.That(sword.Ranged, Is.False);
        }

        [Test]
        public void Notice_NoTarget_DoesNotHold_AndSaysWhy()
        {
            Decide(new NoticeReasoner(), 1, Hold("Notice Block"));
            Decide(new PatrolReasoner(), 2, Hold("Patrol Block"));
            Run(withTarget: false);

            Assert.IsTrue(Reaches("Patrol Block"));
            Assert.AreEqual("no target",
                CombatBrainTask.VerdictsOf(_brain, _rig.Context).Single(verdict => verdict.State.DisplayName == "Notice").WhyNot);
        }

        [Test]
        public void Brain_ADecisionThatRested_IsTakenAgainAfterItsRest()
        {
            Decide(new FightReasoner { Closer = 10f, LetGoAfter = 10f }, 1,
                Block("Strike", Strike(TARGET, 0.5f, 0.2f, 0.2f)));
            Decide(new ChaseReasoner(), 2, Short("Chase Block", 1f));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));

            _victim.transform.position = new Vector3(0.3f, 0f, 0f);       // within the blow's reach now

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits > 0, BehaviorGraphInstance.EMPTY_CHAIN_REST + 2f, STEP),
                "After its rest the Fight is taken again — and its blow lands");
        }

        [Test]
        public void Brain_ADecisionTakenAgainAfterAPause_IsKeptForTheMinimumTimeAgain()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();
            Assert.IsTrue(Reaches("Chase Block"));
            _rig.StepSeconds(1f);

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);           // nothing holds: the agent stands in the Brain
            Assert.IsTrue(Reaches("Brain"));
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);         // Chase again — a decision taken anew
            Assert.IsTrue(_rig.StepUntil(() => _rig.Instance.Decision?.DisplayName == "Chase", 1f, STEP));
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);

            Assert.IsTrue(Reaches("Fight Block"));
            Assert.GreaterOrEqual(TimeOf("Attack") - TimeOf("Chase"), BehaviorGraphInstance.MIN_DECISION_TIME - 1e-4f,
                "Its minimum time started anew: no switch the very next think");
        }

        [Test]
        public void Brain_NoDecisionInCharge_GivesNoBrainTarget_ThoughItSeesOne()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            Run();                                      // the target 5 m off: the fight does not hold

            _rig.StepSeconds(1f);

            Assert.AreEqual("Brain", _rig.StateName, "Set-up: no decision holds");
            Assert.AreSame(_victim.transform, CombatBrainTask.FactsOf(_brain, _rig.Context).Target, "The Brain sees it");
            Assert.IsNull(_rig.Context.BrainTarget, "No decision is in charge: no hidden target for any block");
        }

        [Test]
        public void Brain_EnterStateIntoADecision_TheBrainIsInChargeOfIt()
        {
            Decide(Fight(), 1, Hold("Fight Block"));
            AgentState chase = Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Run();
            _rig.Step(STEP, 1);

            Assert.IsTrue(_rig.Instance.EnterState(chase, _rig.Context), "A loaded save: straight into the decision");

            Assert.AreSame(_brain, _rig.Instance.Brain);
            Assert.AreSame(chase, _rig.Instance.Decision);
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Assert.IsTrue(Reaches("Fight Block"), "The Brain switches from it as from any decision it took");
        }

        [Test]
        public void Brain_Step_TraceOffAndNobodyLooking_AllocatesNothing()
        {
            _rig.Context.ExplainsDecisions = false;
            Decide(Fight(), 1, Block("Strike", Strike("", 2f, 0.2f, 0.2f)));
            Decide(new ChaseReasoner(), 2, Hold("Chase Block"));
            Decide(new PatrolReasoner(), 3, Hold("Patrol Block"));
            Run(track: false);

            // Warm up: every decision taken, every list grown
            for (int i = 0; i < 3; i++)
            {
                _victim.transform.position = new Vector3(FAR, 0f, 0f);
                _rig.StepSeconds(1f);
                _victim.transform.position = new Vector3(NEAR, 0f, 0f);
                _rig.StepSeconds(1f);
                _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
                _rig.StepSeconds(1f);
                _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            }

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            // Phase by phase, so a failure names the step of the Brain that allocates
            var failing = new System.Collections.Generic.List<string>();
            void Phase(string name, NUnit.Framework.TestDelegate step)
            {
                if (!NoAllocation.Constraint.Resolve().ApplyTo(step).IsSuccess)
                    failing.Add(name);
            }

            Phase("far: Chase", () => { _victim.transform.position = new Vector3(FAR, 0f, 0f); _rig.StepSeconds(1f); });
            Phase("far again: Chase holds", () => _rig.StepSeconds(1f));
            Phase("near: Fight", () => { _victim.transform.position = new Vector3(NEAR, 0f, 0f); _rig.StepSeconds(1f); });
            Phase("near again: Fight strikes", () => _rig.StepSeconds(1f));
            // The stimulus is not measured: a write by name hashes the name, and Unity's constraint counts a string hash as
            // an allocation in this editor (the think itself looks nothing up by name — its keys are resolved beforehand)
            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Phase("no target: the first think", () => _rig.Step());
            Phase("no target: the second think", () => _rig.Step());
            Phase("no target: Patrol", () => _rig.StepSeconds(1f));
            Phase("no target again: Patrol holds", () => _rig.StepSeconds(1f));
            Assert.IsEmpty(failing, "Allocating: " + string.Join(" | ", failing));
        }

        [Test]
        public void Brain_TheFirstBlockOfADecision_StartsInTheThinkTheDecisionIsTaken()
        {
            _rig.ThinkInterval = 0.25f;                     // a slow think would show a gap
            Decide(new ChaseReasoner(), 1, Hold("Chase Block"));
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Chase Block", 1f, 0.05f));

            Assert.AreEqual(TimeOf("Chase"), TimeOf("Chase Block"), 1e-5f, "Do leads on at once: no think standing in the decision");
        }

        [Test]
        public void Chain_InstantBlocks_PassWithinOneThink_TheNextBlowIsNotLate()
        {
            const string COUNT = "Blows";
            const float windUp = 0.3f;
            const float recovery = 0.3f;
            const float think = 0.25f;
            const float step = 0.05f;
            _rig.ThinkInterval = think;
            AgentState strike = Block("Strike", Strike("", 2f, windUp, recovery));
            AgentState add = Block("Add", new AddNumberTask(COUNT));
            AgentState branch = Block("Branch", new BranchTask(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 99f)));
            _graph.Then(strike, add);
            _graph.Then(add, branch);
            _graph.OnFail(branch, strike);
            Decide(Fight(), 1, strike);
            _victim.transform.position = new Vector3(NEAR, 0f, 0f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits >= 4, 10f, step));

            List<float> blows = _entered.Where(entry => entry.Name == "Strike").Select(entry => entry.Time).ToList();
            for (int i = 1; i < blows.Count; i++)
            {
                Assert.LessOrEqual(blows[i] - blows[i - 1], windUp + recovery + think + step + 1e-4f,
                    "Add and Branch take no think of their own: the next blow starts within one think");
            }

            Assert.GreaterOrEqual(_rig.Context.Blackboard.Get(COUNT, 0f), 3f, "Every blow was counted");
        }

        // ---------- the graph ----------

        // The places to attack from are the group's business (GroupTests): here every decision does without one
        private static FightReasoner Fight() => new() { Closer = 1.5f, LetGoAfter = 2f };

        // A decision wired to the Brain's Decisions pin with its priority (1 — the most important), its Do pin wired to
        // the first block of its chain; the decision is named by its type
        private AgentState Decide(ReasonerTask decision, int priority, AgentState chain)
        {
            AgentState state = _graph.Decision(decision, priority);
            _graph.Do(state, chain);
            return state;
        }

        // A block in a node of its own name (the tests tell the blocks apart by it)
        private AgentState Block(string name, AgentTask task) => _graph.Block(task, name);

        private AgentState Hold(string name) => Block(name, new WaitTask(100f));

        private AgentState Short(string name, float seconds = 0.2f) => Block(name, new WaitTask(seconds));

        // A blow with the agent's weapon: the agent is given one of a single move with these numbers
        private AttackTask Strike(string target, float reach, float windUp, float recovery, float damage = 1f)
        {
            _rig.GiveWeapon(reach, windUp, recovery, damage);
            return new AttackTask { Target = target, Move = "Normal" };
        }

        private string WhyNot(string decision) =>
            CombatBrainTask.VerdictsOf(_brain, _rig.Context).Single(verdict => verdict.State.DisplayName == decision).WhyNot;

        private void Run(bool withTarget = true, bool track = true)
        {
            _rig.Instance?.Stop(_rig.Context);
            _rig.SetGraph(_graph.Build());
            _rig.Agent.Blackboard.Set(TARGET, withTarget ? _victim.transform : null);
            _rig.Agent.Blackboard.Set(DUMMY, _victim.transform);
            if (!track)
                return;                             // the record of the way allocates: a test that counts bytes runs without it

            _rig.Instance.StateChanged += (_, to) =>
            {
                if (to == null)
                    return;

                _entered.Add((to.DisplayName, _rig.Context.LocalTime));
                foreach (ReasonerVerdict verdict in CombatBrainTask.VerdictsOf(_brain, _rig.Context))
                {
                    if (verdict.WhyNot != null)
                        _whyNotWhenEntered[$"{to.DisplayName}/{verdict.State.DisplayName}"] = verdict.WhyNot;
                }
            };
        }

        private bool Reaches(string state, float seconds = 2f) => _rig.StepUntil(() => _rig.StateName == state, seconds, STEP);

        // How many values the agent's blackboard holds now
        private int BlackboardSize()
        {
            int count = 0;
            for (int i = 0; i < _rig.Context.Blackboard.Count; i++)
            {
                if (_rig.Context.Blackboard.HasValueAt(i))
                    count++;
            }

            return count;
        }

        private List<string> Names() => _entered.Select(entry => entry.Name).ToList();

        private float TimeOf(string state) => _entered.Last(entry => entry.Name == state).Time;
    }
}
