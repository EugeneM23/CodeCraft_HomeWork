using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The flow of a think (whole-plugin recheck, brain; handbook §2.2 — instant nodes pass in one tick; §2.4 — the fixed
    /// order: On Tick, the node's own arrows, then the Brain looks again; §1.2 — the minimum time of a decision): the
    /// Brain looks again in the think a chain moves on, On Tick runs before the node's arrows with its own Delta Time,
    /// the passes of a think are bounded, a chain that did nothing makes its decision rest, a decision leaves cleanly when
    /// the graph stops or another decision is taken, and one graph shared by two agents answers each its own.
    /// </summary>
    public sealed class BrainFlowTests
    {
        private const string ALARM = "Alarm";
        private const float DT = 0.05f;

        private readonly List<Object> _created = new();
        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp() => _entered.Clear();

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

        // ---------- looking again ----------

        [Test]
        public void BehaviorGraphInstance_ChainMovesOnWhileAMoreImportantDecisionHolds_SwitchesInThatThink_TheNextBlockNeverStarts()
        {
            const float THINK = 0.25f;
            var second = new ProbeTask();
            var g = new BrainGraphBuilder();
            g.Do(g.Decision(new FlagReasoner { Flag = ALARM }, 1, "Alarm"), g.Block(new WaitTask(100f), "Alarm Block"));
            AgentState work = g.Decision(new DailyRoutineReasoner(), 2, "Work");
            AgentState first = g.Block(new WaitTask(0.4f), "First");
            g.Do(work, first);
            g.Then(first, g.Block(second, "Second"));
            GraphRig rig = Rig(g, THINK);

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == work, 1f, DT));
            float taken = rig.Context.LocalTime;
            Assert.IsTrue(rig.StepUntil(() => rig.Context.LocalTime >= taken + 0.3f - 1e-4f, 1f, DT));
            rig.Context.Blackboard.Set(ALARM, true);           // after the think at +0.25, before First ends

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Alarm Block", 1f, DT), Path());
            Assert.AreEqual(0, second.Enters, "Second was entered and left in one think: never started");
            Assert.AreEqual(TimeOf("Second"), TimeOf("Alarm Block"), 1e-5f,
                "Switched in the think the chain moved on to Second — the Brain looked again after the chain's arrows");
            Assert.That(TimeOf("Alarm Block") - taken, Is.InRange(0.4f, 0.4f + THINK + 2f * DT),
                "The first think after First (0.4 s) ended");
        }

        // ---------- On Tick ----------

        [Test]
        public void BehaviorGraphInstance_BlockOnTick_DeltaTimeIsTheTimeSinceItsLastRun_ZeroTheFirstTime()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Last", VariableType.Float);
            g.Variable("Sum", VariableType.Float);
            AgentState work = g.Block(new WaitTask(100f), "Work");
            g.Do(g.Always(), work);
            AgentState last = g.Block(new SetNumberTask
            {
                Variable = "Last", Value = BlackboardValue<float>.Variable(BehaviorGraphInstance.DELTA_TIME)
            }, "Last");
            g.OnTick(work, last);
            g.Then(last, g.Block(new AddNumberTask
            {
                Variable = "Sum", Value = BlackboardValue<float>.Variable(BehaviorGraphInstance.DELTA_TIME)
            }, "Sum"));
            GraphRig rig = Rig(g, 0f);
            rig.Context.Blackboard.Set("Last", -1f);

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Work", 1f, DT));
            Assert.AreEqual(-1f, rig.Context.Blackboard.Get("Last", 0f), "Set-up: not run in the think it was entered");
            rig.Step(DT);
            Assert.AreEqual(0f, rig.Context.Blackboard.Get("Last", -1f), "Its first run: no time since the last one");

            int runs = 1;
            for (int i = 0; i < 19; i++)
            {
                rig.Step(DT);
                runs++;
                Assert.AreEqual(DT, rig.Context.Blackboard.Get("Last", 0f), 1e-5f, $"Run {runs}: one think's time");
            }

            Assert.AreEqual(DT * (runs - 1), rig.Context.Blackboard.Get("Sum", 0f), 1e-4f);
            Assert.IsTrue(rig.Context.Blackboard.Has(BehaviorGraphInstance.DELTA_TIME));
        }

        [Test]
        public void BehaviorGraphInstance_OnTick_RunsBeforeTheBlocksOwnArrows_TheyReadWhatItWrote()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Flag", VariableType.Float);
            AgentState work = g.Block(new WaitTask(0.05f), "Work");
            g.Do(g.Always(), work);
            g.OnTick(work, g.Block(new SetNumberTask { Variable = "Flag", Value = 1f }, "Raise"));
            AgentState branch = g.Block(new BranchTask(new CompareCondition("Flag", NumberComparison.GreaterOrEqual, 1f)), "Branch");
            g.Then(work, branch);
            g.Then(branch, g.Block(new WaitTask(100f), "Yes"));
            g.OnFail(branch, g.Block(new WaitTask(100f), "No"));
            GraphRig rig = Rig(g, 0f);

            rig.StepSeconds(1f, 0.1f);

            CollectionAssert.Contains(Names(), "Yes", Path());
            CollectionAssert.DoesNotContain(Names(), "No", "The On Tick wrote the flag before Done led to the Branch");
        }

        [Test]
        public void BehaviorGraphInstance_OnTickLoopOfInstantBlocks_RunsAtMostThirtyTwoNodesAThink()
        {
            const int MOST = 32;
            var g = new BrainGraphBuilder();
            g.Variable("Count", VariableType.Float);
            AgentState work = g.Block(new WaitTask(100f), "Work");
            g.Do(g.Always(), work);
            AgentState count = g.Block(new AddNumberTask("Count"), "Count");
            g.OnTick(work, count);
            g.Then(count, count);
            GraphRig rig = Rig(g, 0f);

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Work", 1f, DT));
            Assert.AreEqual(0f, rig.Context.Blackboard.Get("Count", 0f), "Not run in the think Work was entered");
            for (int think = 1; think <= 5; think++)
            {
                rig.Step(DT);
                Assert.AreEqual(MOST * think, rig.Context.Blackboard.Get("Count", 0f), 1e-3f, "A loop stops at the bound");
                Assert.AreEqual("Work", rig.StateName, "The flow never left Work");
            }
        }

        [Test]
        public void BehaviorGraphInstance_OnTickRandom_FollowsOnlyThePickedOutput()
        {
            (float a, float b, int thinks) = RandomOnTick(seed: 1, weights: new[] { 1f, 0f });
            Assert.AreEqual(thinks, a, 1e-3f, "Weight 0: never");
            Assert.AreEqual(0f, b);

            (float a1, float b1, int thinks1) = RandomOnTick(seed: 5, weights: new[] { 1f, 1f });
            (float a2, float b2, _) = RandomOnTick(seed: 5, weights: new[] { 1f, 1f });
            Assert.AreEqual(thinks1, a1 + b1, 1e-3f, "One output a run, never both");
            Assert.Greater(a1, 0f);
            Assert.Greater(b1, 0f);
            Assert.AreEqual(a1, a2, "One seed, one trace");
            Assert.AreEqual(b1, b2);
        }

        // ---------- the passes of a think ----------

        [Test]
        public void BehaviorGraphInstance_ALoopOfInstantBlocks_PassesAtMostSixteenNodesAThink()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Count", VariableType.Float);
            g.Do(g.Decision(new FlagReasoner { Flag = ALARM }, 1, "Alarm"), g.Block(new WaitTask(100f), "Alarm Block"));
            AgentState always = g.Decision(new DailyRoutineReasoner(), 2, "Always");
            AgentState count = g.Block(new AddNumberTask("Count"), "Count");
            g.Do(always, count);
            g.Then(count, count);
            GraphRig rig = Rig(g, 0f);

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == always, 1f, DT));
            float taken = rig.Context.LocalTime;
            Assert.AreEqual(16f, Count(rig), "15 passes after the decision's own in its think, then its tick");
            rig.Step(DT);
            Assert.AreEqual(33f, Count(rig), "16 passes a think, and the tick of the step");

            Assert.IsTrue(rig.StepUntil(() => rig.Context.LocalTime >= taken + BehaviorGraphInstance.MIN_DECISION_TIME, 1f, DT));
            rig.Context.Blackboard.Set(ALARM, true);
            rig.Step(DT);
            Assert.AreEqual("Alarm Block", rig.StateName, "The bound reached, the Brain still looks again in that think");
        }

        [Test]
        public void BehaviorGraphInstance_AChainOfOnlyInstantBlocks_ItsDecisionRests_NotRunEveryThink()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Count", VariableType.Float);
            g.Do(g.Always(), g.Block(new AddNumberTask("Count"), "Count"));
            AgentState brain = g.Brain;
            GraphRig rig = Rig(g, 0f);
            rig.Context.ExplainsDecisions = true;
            string resting = null;

            for (int i = 0; i < 40; i++)
            {
                rig.Step(DT);
                if (rig.StateName == "Brain")
                    resting ??= CombatBrainTask.VerdictsOf(brain, rig.Context).FirstOrDefault().WhyNot;
            }

            Assert.That(Count(rig), Is.InRange(3f, 4f),
                $"One run, then {BehaviorGraphInstance.EMPTY_CHAIN_REST} s of rest standing in the Brain — not a run a think");
            Assert.AreEqual("did nothing a moment ago — rests", resting);
        }

        // ---------- leaving a decision ----------

        [Test]
        public void BehaviorGraphInstance_Stop_TheDecisionInChargeLeaves_DecisionLeftOnce_ItsChoicesCleared()
        {
            AbilityAsset slam = Ability("Slam", windUp: 1f, recovery: 1f, max: 5f, cooldown: 5f);
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            AgentState use = g.Decision(new UseAbilityReasoner { Ability = slam }, 1);
            g.Chain(use, new UseAbilityTask(), new WaitTask(100f));
            GraphRig rig = Rig(g, 0f);
            rig.Agent.Blackboard.Set("Target", rig.CreateTargetAt(new Vector3(2f, 0f, 0f)));
            int left = 0;
            rig.Context.DecisionLeft += () => left++;
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == use && rig.Abilities.IsRunning("Slam"), 1f, DT));
            Assert.AreSame(slam, rig.Context.ChosenAbility, "Set-up: the decision handed its ability to its chain");

            rig.Instance.Stop(rig.Context);

            Assert.AreEqual(1, left, "The decision in charge left — once");
            Assert.IsNull(rig.Instance.Decision);
            Assert.IsNull(rig.Instance.Brain);
            Assert.IsNull(rig.Context.BrainTarget);
            Assert.IsNull(rig.Context.ChosenAbility, "What it handed its chain is taken back");
            Assert.IsFalse(rig.Instance.IsRunning);
            Assert.IsFalse(rig.Abilities.IsRunning("Slam"), "The ability on its way is cancelled");

            rig.Step(DT);
            Assert.AreEqual("Brain", rig.StateName, "Started anew: in the Brain");
            Assert.AreEqual(1, left);
        }

        [Test]
        public void BehaviorGraphInstance_SwitchBetweenTwoAbilityDecisions_TheNewOnesAbilityIsHandedToItsChain()
        {
            AbilityAsset bolt = Ability("Bolt", windUp: 0.1f, recovery: 0.1f, max: 3f, cooldown: 0.5f);
            AbilityAsset slam = Ability("Slam", windUp: 0.1f, recovery: 0.1f, max: 10f, cooldown: 0.5f);
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            AgentState boltDecision = g.Decision(new UseAbilityReasoner { Ability = bolt }, 1, "Bolt");
            g.Chain(boltDecision, new UseAbilityTask(), new WaitTask(0.3f));
            AgentState slamDecision = g.Decision(new UseAbilityReasoner { Ability = slam }, 2, "Slam");
            g.Chain(slamDecision, new UseAbilityTask(), new WaitTask(0.3f));
            GraphRig rig = Rig(g, 0f);
            Transform target = rig.CreateTargetAt(new Vector3(8f, 0f, 0f));
            rig.Agent.Blackboard.Set("Target", target);
            AbilityRunner boltRunner = rig.Agent.AbilityRunners.Single(runner => runner.Id == "Bolt");
            AbilityRunner slamRunner = rig.Agent.AbilityRunners.Single(runner => runner.Id == "Slam");

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == slamDecision, 1f, DT), "Far: Slam");
            Assert.AreSame(slam, rig.Context.ChosenAbility);
            rig.MoveTargetTo(new Vector3(2f, 0f, 0f));

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == boltDecision, 2f, DT), "Near: Bolt");
            int slamUses = slamRunner.Uses;
            Assert.AreSame(bolt, rig.Context.ChosenAbility, "Handed to the new chain: the old one's leaving did not clear it");
            Assert.IsTrue(rig.StepUntil(() => boltRunner.Uses == 1, 1f, DT), "Its chain used Bolt");
            Assert.AreEqual(slamUses, slamRunner.Uses, "…not Slam");
        }

        [Test]
        public void CombatBrainTask_NoDecisionHoldsAnyMore_TheBodyIsStoppedAndNavigationCleared()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            g.Do(g.Decision(new ChaseReasoner(), 1), g.Block(new ChaseTask { StopDistance = 0.5f }, "Chase Block"));
            GraphRig rig = Rig(g, 0f);
            rig.Agent.Blackboard.Set("Target", rig.CreateTargetAt(new Vector3(5f, 0f, 0f)));
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Chase Block", 1f, DT));
            rig.Step(DT, 4);
            Assert.Greater(rig.Actuator.Position.x, 0.1f, "Set-up: on its way");
            int stops = rig.Actuator.Stops;

            rig.Agent.Blackboard.Set("Target", (Transform)null);
            rig.Step(DT);

            Assert.AreEqual("Brain", rig.StateName, "Nothing holds: back in the Brain");
            Assert.Greater(rig.Actuator.Stops, stops, "The body is stopped");
            Assert.AreEqual(NavStatus.Idle, rig.Context.Navigation.Status, "No way to follow any more");
            Vector3 standing = rig.Actuator.Position;
            rig.Step(DT, 5);
            Assert.AreEqual(standing, rig.Actuator.Position, "It stands");
        }

        [Test]
        public void BehaviorGraphInstance_DecisionWithNothingBehindItsDo_TheAgentStandsInIt_ItsOwnOnTickRuns()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Count", VariableType.Float);
            AgentState always = g.Always("Always");
            g.OnTick(always, g.Block(new AddNumberTask("Count"), "Count"));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f), "Patrol Block"));
            BehaviorGraph graph = g.Build();
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(graph);
            Track(rig);

            for (int i = 0; i < 20; i++)
            {
                rig.Step(DT);
                if (i >= 1)
                {
                    Assert.AreEqual("Always", rig.StateName, $"Step {i + 1}: it stands in the decision");
                    Assert.AreEqual(always, rig.Instance.Decision);
                }
            }

            CollectionAssert.DoesNotContain(Names(), "Patrol Block");
            Assert.AreEqual(18f, Count(rig), "Its On Tick ran once a think from the third (the first starts, the second takes)");

            List<GraphIssue> issues = Validate(graph);
            GraphIssue noDo = issues.FirstOrDefault(issue => issue.Code == GraphIssueCode.NoChain && issue.RecordId == always.Id);
            Assert.AreEqual(GraphIssueCode.NoChain, noDo.Code, "The empty Do is named: " + string.Join("\n", issues));
            Assert.IsFalse(noDo.IsError, "A warning: the decision does run its On Tick");
        }

        [Test]
        public void BehaviorGraphInstance_SameDecisionRetakenAfterItsChain_NoDecisionLeft_ItsSetParameterStays()
        {
            var go = new GameObject("Numbered") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            StatusTests.Numbers numbers = go.AddComponent<StatusTests.Numbers>();
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.ThinkInterval = 0f;
            var actuator = new TestActuator();
            character.SetParts(actuator, new DirectNavigator(actuator));
            character.Initialize();
            float own = numbers.Values[0];
            GameObject foe = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(foe);

            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            AgentState watch = g.Decision(new WatchReasoner(), 1);
            AgentState faster = g.Block(new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 1.5f });
            g.Do(watch, faster);
            g.Then(faster, g.Block(new WaitTask(0.2f), "Pause"));    // the chain ends: Watch is taken again
            character.SetGraph(g.Build());
            character.Blackboard.Set("Target", foe.transform);
            int left = 0;
            character.Context.DecisionLeft += () => left++;
            int retaken = 0;
            character.Graph.StateChanged += (_, to) => retaken += to == watch ? 1 : 0;

            bool faster1 = false;
            for (int i = 0; i < 40; i++)
            {
                character.Step(DT);
                faster1 |= Mathf.Abs(numbers.Values[0] - own * 1.5f) < 1e-4f;
                if (faster1)
                    Assert.AreEqual(own * 1.5f, numbers.Values[0], 1e-4f,
                        $"Step {i + 1}: half as fast again — never back to its own between re-takes, never compounded");
            }

            Assert.IsTrue(faster1, "Set-up: the parameter was set");
            Assert.Greater(retaken, 3, "Set-up: Watch was taken again and again");
            Assert.AreEqual(0, left, "The same decision all the while: it never left");

            character.Blackboard.Set("Target", (Transform)null);
            character.Step(DT);
            Assert.AreEqual(own, numbers.Values[0], 1e-4f, "It left: the body's own value is back");
            Assert.AreEqual(1, left);
        }

        [Test]
        public void BehaviorGraphInstance_ARepeatingBlockFailsWithNoFailedArrow_IsNotRestarted_TheBrainChoosesAgain()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            AgentState fight = g.Decision(new FightReasoner { Closer = 10f, LetGoAfter = 10f }, 1, "Fight");
            g.Do(fight, g.Block(new AttackTask { Move = "Normal" }, "Strike"));
            g.Do(g.Decision(new ChaseReasoner(), 2), g.Block(new WaitTask(100f), "Chase Block"));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.GiveWeapon(reach: 2f, windUp: 0.2f, recovery: 0.2f);
            rig.SetGraph(g.Build());
            Track(rig);
            var victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(victim);
            victim.transform.position = new Vector3(1f, 0f, 0f);
            HitProbe hits = victim.AddComponent<HitProbe>();
            rig.Agent.Blackboard.Set("Target", victim.transform);
            string fightWhenChasing = null;
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to?.DisplayName == "Chase Block")
                    fightWhenChasing = CombatBrainTask.VerdictsOf(g.Brain, rig.Context)
                        .Single(verdict => verdict.State == fight).WhyNot;
            };

            Assert.IsTrue(rig.StepUntil(() => hits.Hits == 1, 2f, DT), "Set-up: the first blow lands");
            victim.transform.position = new Vector3(5f, 0f, 0f);     // out of the blow's reach, within the decision's

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Chase Block", 1f, DT), Path());
            Assert.AreEqual(1, hits.Hits, "No blow out of reach");
            Assert.AreEqual("did nothing a moment ago — rests", fightWhenChasing,
                "Taken again (its chain had struck), its blow failed at once: it rests, the Brain chooses the chase");
        }

        // ---------- the graph ----------

        [Test]
        public void BehaviorGraph_ABlockInTwoChains_BelongsToTheFirstWiredDecision()
        {
            var g = new BrainGraphBuilder();
            AgentState first = g.Decision(new DailyRoutineReasoner(), 1, "First");
            AgentState second = g.Decision(new PatrolReasoner(), 2, "Second");
            AgentState x = g.Block(new WaitTask(0.1f), "X");
            AgentState shared = g.Block(new WaitTask(100f), "Shared");
            g.Do(first, x);
            g.Then(x, shared);
            g.Do(second, shared);
            BehaviorGraph graph = g.Build();

            Assert.AreSame(first, graph.ChainOwnerOf(shared), "The first decision wired to the Brain owns it");
            Assert.IsTrue(graph.Runs(shared));

            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(graph);
            rig.Step(DT);
            Assert.IsTrue(rig.Instance.EnterState(shared, rig.Context));
            Assert.AreSame(first, rig.Instance.Decision);
            Assert.AreSame(shared, rig.Instance.Current);
        }

        [Test]
        public void AgentCondition_SharedGraph_TwoAgentsWithTheSameThinkStamp_EachGetsItsOwnAnswer()
        {
            var g = new BrainGraphBuilder();
            g.Variable("X", VariableType.Float);
            AgentState branch = g.Block(new BranchTask(new CompareCondition("X", NumberComparison.GreaterOrEqual, 1f)), "Branch");
            g.Do(g.Always(), branch);
            g.Then(branch, g.Block(new WaitTask(100f), "Yes"));
            g.OnFail(branch, g.Block(new WaitTask(100f), "No"));
            BehaviorGraph graph = g.Build();
            var a = new GraphRig(trace: false) { ThinkInterval = 0f };
            var b = new GraphRig(trace: false) { ThinkInterval = 0f };
            a.SetGraph(graph);
            b.SetGraph(graph);
            a.Context.Blackboard.Set("X", 1f);
            b.Context.Blackboard.Set("X", 0f);
            var aEntered = new List<string>();
            var bEntered = new List<string>();
            a.Instance.StateChanged += (_, to) => aEntered.Add(to?.DisplayName);
            b.Instance.StateChanged += (_, to) => bEntered.Add(to?.DisplayName);

            for (int i = 0; i < 4; i++)
            {
                a.Step(DT);
                b.Step(DT);                         // the same think stamps: one condition node asked by both
            }

            Assert.AreEqual("Yes", a.StateName);
            Assert.AreEqual("No", b.StateName);
            for (int i = 0; i < 4; i++)
            {
                b.Step(DT);
                a.Step(DT);
            }

            Assert.AreEqual("Yes", a.StateName);
            Assert.AreEqual("No", b.StateName);
            CollectionAssert.DoesNotContain(aEntered, "No");
            CollectionAssert.DoesNotContain(bEntered, "Yes");
        }

        // ---------- the rig ----------

        private GraphRig Rig(BrainGraphBuilder g, float think)
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = think };
            rig.SetGraph(g.Build());
            Track(rig);
            return rig;
        }

        private void Track(GraphRig rig) =>
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, rig.Context.LocalTime));
            };

        private (float A, float B, int Thinks) RandomOnTick(int seed, float[] weights)
        {
            var g = new BrainGraphBuilder();
            g.Variable("A", VariableType.Float);
            g.Variable("B", VariableType.Float);
            AgentState work = g.Block(new WaitTask(100f), "Work");
            g.Do(g.Always(), work);
            var pick = new PickRandomTask { Outputs = weights.Select(weight => new RandomOutput { Weight = weight }).ToList() };
            AgentState random = g.Block(pick, "Random");
            g.OnTick(work, random);
            g.Out(random, 0, g.Block(new AddNumberTask("A"), "Add A"));
            g.Out(random, 1, g.Block(new AddNumberTask("B"), "Add B"));
            var rig = new GraphRig(seed, trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());
            int thinks = 0;
            for (int i = 0; i < 20; i++)
            {
                bool inWork = rig.StateName == "Work";
                rig.Step(DT);
                if (inWork)
                    thinks++;
            }

            return (rig.Context.Blackboard.Get("A", 0f), rig.Context.Blackboard.Get("B", 0f), thinks);
        }

        private AbilityAsset Ability(string name, float windUp, float recovery, float max, float cooldown)
        {
            AbilityAsset ability = AbilityAsset.Create(name, new RangeCondition { Min = 0f, Max = max },
                new CooldownCondition { Seconds = cooldown }, new SelfDelivery());
            ability.hideFlags = HideFlags.HideAndDontSave;
            ability.Timing = new AbilityTiming { WindUp = windUp, Recovery = recovery };
            ability.Queue.AtOnce = 0;
            _created.Add(ability);
            return ability;
        }

        private List<GraphIssue> Validate(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return GraphValidator.Validate(asset);
        }

        private static float Count(GraphRig rig) => rig.Context.Blackboard.Get("Count", 0f);

        private List<string> Names() => _entered.Select(entry => entry.Name).ToList();

        private float TimeOf(string state) => _entered.Last(entry => entry.Name == state).Time;

        private string Path() => string.Join(" → ", _entered.Select(entry => $"{entry.Name}@{entry.Time:0.00}"));

        /// <summary>A block that runs until it is taken away, counting its starts.</summary>
        private sealed class ProbeTask : AgentTask
        {
            public int Enters;

            protected override void OnEnter(AgentContext context) => Enters++;

            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        /// <summary>A decision that holds while a flag of the blackboard is on (it needs no target).</summary>
        private sealed class FlagReasoner : ReasonerTask
        {
            public string Flag;

            public override bool NeedsTarget => false;

            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                bool on = query.Context != null && query.Context.Blackboard.Get(Flag, false);
                whyNot = on ? null : "the flag is off";
                return on;
            }
        }
    }
}
