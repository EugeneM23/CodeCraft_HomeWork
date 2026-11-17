using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The ability decisions are questions (whole-plugin recheck, brain; invariant 6 — a question makes no state):
    /// asked every think without being taken, Use Ability and Use Any Ability make no fight round the target (no
    /// Engagement is made — AgentRoster.FindEngagement), and asking Use Any Ability draws nothing from the agent's
    /// random even when an ability of its set is scored by a Random axis.
    /// </summary>
    public sealed class BrainAbilityQuestionTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

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

        [Test]
        public void UseAbilityReasoner_AskedEveryThinkButNotTaken_MakesNoEngagementAroundTheTarget()
        {
            AbilityAsset bolt = Bolt();
            AskedNotTaken(new UseAbilityReasoner { Ability = bolt }, out AIWorld world, out Transform target,
                out GraphRig rig, out AgentState brain);

            Assert.IsNull(world.Roster.FindEngagement(target), "Nobody asked for a place: no fight round the target");
            Assert.IsFalse(world.Roster.Engagements.Any());
            ReasonerVerdict verdict = CombatBrainTask.VerdictsOf(brain, rig.Context)
                .Single(each => each.State.Reasoner is UseAbilityReasoner);
            Assert.IsTrue(verdict.Holds, $"Set-up: ready, with a place to be had ({verdict.WhyNot})");
            Assert.IsFalse(verdict.Won);
        }

        [Test]
        public void UseAnyAbilityReasoner_AskedEveryThinkButNotTaken_MakesNoEngagementAroundTheTarget()
        {
            AbilityAsset bolt = Bolt();
            AskedNotTaken(new UseAnyAbilityReasoner(bolt), out AIWorld world, out Transform target,
                out GraphRig rig, out AgentState brain);

            Assert.IsNull(world.Roster.FindEngagement(target), "Nobody asked for a place: no fight round the target");
            Assert.IsFalse(world.Roster.Engagements.Any());
            ReasonerVerdict verdict = CombatBrainTask.VerdictsOf(brain, rig.Context)
                .Single(each => each.State.Reasoner is UseAnyAbilityReasoner);
            Assert.IsTrue(verdict.Holds, $"Set-up: ready, with a place to be had ({verdict.WhyNot})");
        }

        [Test]
        public void AgentAbilities_APoolWithNoAtOnceOfItsOwn_TheTargetsSay_NoFightIsMadeToKnowIt()
        {
            // A game's definition with a pool and no number of its own: the target says how many (its Attack Slots)
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.AddAbility("Bolt", maxRange: 9f);
            AbilityDefinition bolt = rig.Abilities.DefinitionOf("Bolt");
            bolt.TokenPool = "Bolt";
            bolt.TokenCapacity = 0;
            Transform target = rig.CreateTargetAt(new Vector3(6f, 0f, 0f));
            var slots = target.gameObject.AddComponent<AttackSlotCapacity>();

            slots.SetPool("Bolt", 0);
            Assert.AreEqual(AbilityBlock.TurnTaken, rig.Abilities.BlockOf("Bolt", rig.Context, withPlace: true),
                "The target takes nobody of the pool: no place to be had");
            slots.SetPool("Bolt", 1);
            Assert.AreEqual(AbilityBlock.None, rig.Abilities.BlockOf("Bolt", rig.Context, withPlace: true),
                "One place, nobody holds it: to be had");
            Assert.IsNull(world.Roster.FindEngagement(target), "…and asking made no fight round the target");
        }

        [Test]
        public void AgentAbilities_AnAbilityWithItsOwnAtOnce_APlaceIsToBeHad_AsItsAskWouldSetIt()
        {
            AbilityAsset bolt = Bolt();
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = bolt }, 1), new UseAbilityTask());
            rig.SetGraph(g.Build());
            Transform target = rig.CreateTargetAt(new Vector3(6f, 0f, 0f));
            target.gameObject.AddComponent<AttackSlotCapacity>().SetPool("Bolt", 0);

            Assert.AreEqual(AbilityBlock.None, rig.Abilities.BlockOf("Bolt", rig.Context, withPlace: true),
                "Its At Once is the pool's number when it asks (whatever the target says): nobody holds it yet");
            Assert.IsNull(world.Roster.FindEngagement(target), "…and asking made no fight round the target");
        }

        [Test]
        public void UseAnyAbilityReasoner_AnAbilityScoredByARandomAxis_AskingDrawsNothing()
        {
            AbilityAsset dice = MakeAbility("Dice",
                new RangeCondition { Min = 0f, Max = 5f }, new CooldownCondition { Seconds = 0f }, new SelfDelivery(),
                new ScoreCondition
                {
                    Considerations = new[] { new Consideration { Input = ConsiderationInput.Random, Required = true } }
                });
            dice.Queue.AtOnce = 0;
            var any = new UseAnyAbilityReasoner(dice);
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Chain(g.Decision(any, 1), new UseAbilityTask());
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());
            Transform target = rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            ulong before = rig.Context.RandomState;

            var answers = new List<bool>();
            for (int i = 0; i < 10; i++)
                answers.Add(any.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false), out _));

            Assert.AreEqual(before, rig.Context.RandomState, "Asking drew nothing from the agent's random");
            Assert.AreEqual(1, answers.Distinct().Count(), "The same question, the same answer");
        }

        [Test]
        public void UseAnyAbilityReasoner_ARandomAxis_WhatTheQuestionSaw_ThePickSeesToo()
        {
            // The answer of the question and the pick made right after it go by the same numbers: a decision that is
            // taken because the question said yes picks an ability (never none)
            for (int seed = 1; seed <= 20; seed++)
            {
                AbilityAsset dice = MakeAbility($"Dice{seed}",
                    new RangeCondition { Min = 0f, Max = 5f }, new CooldownCondition { Seconds = 0f }, new SelfDelivery(),
                    new ScoreCondition
                    {
                        Considerations = new[]
                        {
                            new Consideration
                            {
                                Input = ConsiderationInput.Random, Required = true, InputMin = 0.5f, InputMax = 1f
                            }
                        }
                    });
                dice.Queue.AtOnce = 0;
                var any = new UseAnyAbilityReasoner(dice);
                var g = new BrainGraphBuilder();
                g.Chain(g.Decision(any, 1), new UseAbilityTask());
                var rig = new GraphRig(seed, trace: false) { ThinkInterval = 0f };
                rig.SetGraph(g.Build());
                Transform target = rig.CreateTargetAt(new Vector3(1f, 0f, 0f));

                bool holds = any.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false), out _);
                any.Taken(rig.Context, TargetFacts.Of(rig.Context, target));

                Assert.AreEqual(holds, any.PickOf(rig.Context) != null,
                    $"Seed {seed}: the question said {(holds ? "yes" : "no")}, the pick must say the same");
            }
        }

        // ---------- the rig ----------

        // A Watch more important than the ability decision: the decision is asked every think and never taken
        private void AskedNotTaken(ReasonerTask decision, out AIWorld world, out Transform target, out GraphRig rig,
            out AgentState brain)
        {
            world = new AIWorld();
            rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Context.ExplainsDecisions = true;
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new WatchReasoner(), 1), g.Block(new WaitTask(100f), "Watch Block"));
            g.Chain(g.Decision(decision, 2), new UseAbilityTask());
            brain = g.Brain;
            rig.SetGraph(g.Build());
            target = rig.CreateTargetAt(new Vector3(6f, 0f, 0f));
            rig.Agent.Blackboard.Set(TARGET, target);

            for (float t = 0f; t < 1f; t += DT)
            {
                world.BeginStep(DT);
                rig.Step(DT);
            }

            Assert.AreEqual("Watch Block", rig.StateName, "Set-up: the Watch is taken, the ability decision only asked");
            rig.Agent.gameObject.SetActive(false);
        }

        // An ability of one pool of its own (At Once 1 — the default queue), ready at 6 m
        private AbilityAsset Bolt()
        {
            AbilityAsset bolt = MakeAbility("Bolt", new RangeCondition { Min = 0f, Max = 9f },
                new CooldownCondition { Seconds = 1f }, new SelfDelivery());
            Assert.AreEqual(1, bolt.Queue.AtOnce, "Set-up: the default queue — one at a time, a pool of its own");
            return bolt;
        }

        private AbilityAsset MakeAbility(string name, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            ability.hideFlags = HideFlags.HideAndDontSave;
            ability.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            _created.Add(ability);
            return ability;
        }
    }
}
