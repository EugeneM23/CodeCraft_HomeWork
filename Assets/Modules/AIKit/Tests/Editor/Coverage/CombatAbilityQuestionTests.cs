using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The ability decisions asked and taken (the step 90 recheck of the fight): asking whether an ability is ready — its
    /// pause, its range, a free turn in its pool — makes no fight round the target (a question makes no state); an
    /// ability with no range part reaches from anywhere, and so does its turn; the only ready ability of a set is used
    /// again after its pause whatever its repeat penalty; a timer of the graph keeps the pause instead of the ability's
    /// own; a pick that plays keeps its decision; a required axis with no data vetoes one ability, not the decision.
    /// </summary>
    public sealed class CombatAbilityQuestionTests
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

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        // An agent of the world that thinks every step, awake so the roster counts its requests
        private static GraphRig Fighter(AIWorld world)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            return rig;
        }

        // The Brain sees TARGET; a decision that always holds and is never done (priority 1) — the one asked only (2)
        private static BehaviorGraph AskedOnly(ReasonerTask asked)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new DailyRoutineReasoner(), 1), g.Block(new WaitTask(100f), "Stand"));
            g.Do(g.Decision(asked, 2), g.Block(new UseAbilityTask(), "Use"));
            return g.Build();
        }

        // The Brain sees TARGET; the decision and a Use Ability block of its chain
        private static BehaviorGraph Taken(ReasonerTask decision)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(decision, 1), g.Block(new UseAbilityTask(), "Use"));
            return g.Build();
        }

        private static void Run(AIWorld world, float seconds, GraphRig rig)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                world?.BeginStep(DT);
                rig.Step(DT);
            }
        }

        private static AbilityRunner RunnerOf(GraphRig rig, string id)
        {
            foreach (AbilityRunner runner in rig.Agent.AbilityRunners)
            {
                if (runner.Id == id)
                    return runner;
            }

            Assert.Fail($"{id}: no runner prepared");
            return null;
        }

        // The grenade of the lab's skeletons: 0–9 m, a pause of 6 s, a lob; one at a time (its default queue)
        private static AbilityAsset Grenade() =>
            GraphRig.MakeAbility("Grenade", new RangeCondition { Min = 0f, Max = 9f }, new CooldownCondition { Seconds = 6f },
                new LobDelivery());

        // ---------- a question makes no fight ----------

        [Test]
        public void UseAbilityReasoner_AskedInAWorld_MakesNoEngagementRoundTheTarget()
        {
            var world = new AIWorld();
            GraphRig rig = Fighter(world);
            Transform target = Thing("Target", new Vector3(0f, 0f, 5f));
            var decision = new UseAbilityReasoner { Ability = Grenade() };
            rig.SetGraph(AskedOnly(decision));
            rig.Agent.Blackboard.Set(TARGET, target);

            bool holds = decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false),
                out string why);

            Assert.IsTrue(holds, $"Set-up: ready at 5 m (0–9 m), rested, a free turn — {why}");
            Assert.IsNull(world.Roster.FindEngagement(target), "The question made no engagement round the target");
            Run(world, 1f, rig);
            CollectionAssert.IsEmpty(world.Roster.Engagements,
                "Asked every think and never taken: nobody asked a place, there is no fight round it");
        }

        [Test]
        public void UseAnyAbilityReasoner_AskedInAWorld_MakesNoEngagement()
        {
            var world = new AIWorld();
            GraphRig rig = Fighter(world);
            Transform target = Thing("Target", new Vector3(0f, 0f, 5f));
            var decision = new UseAnyAbilityReasoner(Grenade());
            rig.SetGraph(AskedOnly(decision));
            rig.Agent.Blackboard.Set(TARGET, target);

            bool holds = decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false),
                out string why);

            Assert.IsTrue(holds, $"Set-up: its one ability is ready — {why}");
            Assert.IsNull(world.Roster.FindEngagement(target), "The question made no engagement round the target");
            Run(world, 1f, rig);
            CollectionAssert.IsEmpty(world.Roster.Engagements, "Asked every think, never taken: still no fight round it");
        }

        // ---------- an ability with no range part ----------

        [Test]
        public void UseAbility_AnAbilityWithNoRangePart_TakesItsTurnBeyondTheTokensWinRadius()
        {
            var world = new AIWorld();
            GraphRig rig = Fighter(world);
            float far = new AttackTokens(null).WinRadius + 4f;
            Transform target = Thing("Target", new Vector3(0f, 0f, far));
            AbilityAsset heal = GraphRig.MakeAbility("Heal", new SelfDelivery(), new CooldownCondition { Seconds = 1f });
            heal.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            rig.SetGraph(Taken(new UseAbilityReasoner { Ability = heal }));
            rig.Agent.Blackboard.Set(TARGET, target);

            Run(world, 1f, rig);

            Assert.GreaterOrEqual(RunnerOf(rig, "Heal").Uses, 1,
                "No range part — it reaches from anywhere, and so does its turn in its pool: it heals");
        }

        [Test]
        public void UseAbilityTask_AnAbilityWithNoRangePart_FarBeyondTheWinRadius_ItsAskIsFiledThenPlays()
        {
            var world = new AIWorld();
            GraphRig rig = Fighter(world);
            float far = new AttackTokens(null).WinRadius + 4f;
            Transform target = Thing("Target", new Vector3(0f, 0f, far));
            AbilityAsset heal = GraphRig.MakeAbility("Heal", new SelfDelivery(), new CooldownCondition { Seconds = 1f });
            heal.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            var task = new UseAbilityTask(heal) { Target = TARGET };
            rig.SetGraph(GraphRig.OneState(task));                  // the graph names it: the runner is prepared
            rig.Agent.Blackboard.Set(TARGET, target);

            Assert.AreEqual(TaskStatus.Running, task.Tick(rig.Context, DT), "Filed: it waits for its turn, not refused");
            world.BeginStep(DT);
            Assert.AreEqual(TaskStatus.Running, task.Tick(rig.Context, DT));
            Assert.AreEqual(1, RunnerOf(rig, "Heal").Uses, "Its turn came: it plays");
        }

        // ---------- the repeat penalty ----------

        // Range 0–5 m, a pause of 0.5 s, 0.1 + 0.1 s of play, any number at once, never twice in a row while another is ready
        private static AbilityAsset Penalised(string name, float max)
        {
            AbilityAsset ability = GraphRig.MakeAbility(name, new RangeCondition { Min = 0f, Max = max },
                new CooldownCondition { Seconds = 0.5f }, new StrikeDelivery { Reach = max });
            ability.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            ability.Queue.AtOnce = 0;
            ability.Queue.RepeatPenalty = 1f;
            return ability;
        }

        [Test]
        public void UseAnyAbility_RepeatPenaltyOne_TheOnlyReadyAbility_IsUsedAgainAfterItsPause()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            Transform target = Thing("Target", new Vector3(0f, 0f, 3f));
            rig.SetGraph(Taken(new UseAnyAbilityReasoner(Penalised("Grenade", 5f))));
            rig.Agent.Blackboard.Set(TARGET, target);

            rig.StepSeconds(2f);

            Assert.GreaterOrEqual(RunnerOf(rig, "Grenade").Uses, 2,
                "Nothing else is ready: the penalty for using it twice in a row does not apply (0.2 s of play + 0.5 s pause)");
        }

        [Test]
        public void UseAnyAbility_RepeatPenaltyOne_TheOtherOfTheSetNeverReady_TheOneIsUsedAgain()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            Transform target = Thing("Target", new Vector3(0f, 0f, 3f));
            rig.SetGraph(Taken(new UseAnyAbilityReasoner(Penalised("Grenade", 5f), Penalised("Kick", 1f))));
            rig.Agent.Blackboard.Set(TARGET, target);

            rig.StepSeconds(2f);

            Assert.AreEqual(0, RunnerOf(rig, "Kick").Uses, "Set-up: the kick never reaches 3 m");
            Assert.GreaterOrEqual(RunnerOf(rig, "Grenade").Uses, 2, "The kick is not ready: the grenade is used again");
        }

        // ---------- whose pause ----------

        // ---------- the pick of Use Any Ability ----------

        [Test]
        public void UseAnyAbility_ThePickPlays_HoldsWhileCurrent_NotTakenAnewOutOfRange()
        {
            var rig = new GraphRig(trace: false);
            Transform target = Thing("Target", new Vector3(0f, 0f, 2f));
            AbilityAsset slam = GraphRig.MakeAbility("Slam", new RangeCondition { Min = 0f, Max = 5f },
                new StrikeDelivery { Reach = 5f });
            slam.Timing = new AbilityTiming { WindUp = 1f, Recovery = 1f };
            slam.Queue.AtOnce = 0;
            var decision = new UseAnyAbilityReasoner(slam);
            rig.SetGraph(Taken(decision));                          // its runner is prepared; the graph is not stepped
            rig.Agent.Blackboard.Set(TARGET, target);

            decision.Taken(rig.Context, TargetFacts.Of(rig.Context, target));
            Assert.AreSame(slam, decision.PickOf(rig.Context), "Set-up: taken, it picked the slam");
            using (rig.Context.AimAt(target))
                Assert.IsTrue(rig.Abilities.TryUse("Slam", rig.Context), "Set-up: it plays");
            target.position = new Vector3(0f, 0f, 30f);
            TargetFacts far = TargetFacts.Of(rig.Context, target);

            Assert.IsTrue(decision.Holds(new ReasonerQuery(rig.Context, far, true, false), out _),
                "Its pick plays: the decision stays taken, wherever the target went");
            Assert.IsFalse(decision.Holds(new ReasonerQuery(rig.Context, far, false, false), out string why),
                "Not taken anew: nothing of its set is ready out there");
            Assert.AreEqual("none ready", why);
        }

        [Test]
        public void ScoreCondition_RequiredAxisWithoutData_VetoesIt_UseAnyAbilityPicksTheOther()
        {
            var rig = new GraphRig(trace: true);
            Transform target = Thing("Target", new Vector3(0f, 0f, 2f));
            var axis = new Consideration
            {
                Name = "Morale", Input = ConsiderationInput.Variable, Variable = "Morale", InputMin = 0f, InputMax = 1f,
                Required = true
            };
            AbilityAsset rally = GraphRig.MakeAbility("Rally", new ScoreCondition { Considerations = new[] { axis } },
                new StrikeDelivery { Reach = 5f });
            AbilityAsset jab = GraphRig.MakeAbility("Jab", new StrikeDelivery { Reach = 5f });
            rally.Queue.AtOnce = 0;
            jab.Queue.AtOnce = 0;
            var decision = new UseAnyAbilityReasoner(rally, jab);
            rig.SetGraph(Taken(decision));
            rig.Agent.Blackboard.Set(TARGET, target);
            TargetFacts facts = TargetFacts.Of(rig.Context, target);

            Assert.IsTrue(decision.Holds(new ReasonerQuery(rig.Context, facts, false, false), out string why),
                $"The other one is ready: the decision holds — {why}");
            for (int i = 0; i < 20; i++)
            {
                decision.Taken(rig.Context, facts);
                Assert.AreSame(jab, decision.PickOf(rig.Context), "The vetoed one is never picked");
                decision.LeftCharge(rig.Context);
            }

            bool vetoed = false;
            for (int i = 0; i < rig.Context.Trace.Count; i++)
                vetoed |= rig.Context.Trace[i].Kind == DecisionKind.AbilityVetoed && rig.Context.Trace[i].AbilityId == "Rally";
            Assert.IsTrue(vetoed, "The trace names the veto (no data for a required axis), not a zero");
        }
    }
}
