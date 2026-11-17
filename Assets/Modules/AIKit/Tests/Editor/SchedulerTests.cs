using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The scheduler rations decisions and never steps an agent: a cap per step by count (the same on every machine),
    /// first decisions spread over the think interval, and an agent stepped by its own driver exactly once per step.
    /// </summary>
    public class SchedulerTests
    {
        private const float DT = 0.02f;

        [Test]
        public void MaxDecisionsPerStep_CapsEveryStep_AndNobodyStarves()
        {
            List<string> log = Crowd(10, 2, 60, out List<GraphRig> rigs);

            foreach (GraphRig rig in rigs)
                Assert.Greater(rig.Agent.DecisionCount, 1, "Every agent must get its turn to decide");

            foreach (string step in log)
                Assert.LessOrEqual(step.Split(',').Length - 1, 2, $"More than two decisions in one step: {step}");
        }

        [Test]
        public void MaxDecisionsPerStep_WhoThinks_IsReproducible()
        {
            List<string> first = Crowd(10, 2, 60, out _);
            List<string> second = Crowd(10, 2, 60, out _);

            CollectionAssert.AreEqual(first, second, "The same crowd must decide in the same order");
        }

        [Test]
        public void Register_SpreadsTheFirstDecisions()
        {
            var world = new AIWorld();
            var rigs = new List<GraphRig>();
            for (int i = 0; i < 8; i++)
                rigs.Add(Agent(world));

            world.BeginStep();
            foreach (GraphRig rig in rigs)
                rig.Step(DT);

            Assert.Less(world.Scheduler.DecisionsThisStep, rigs.Count,
                "Agents registered together must not all decide on the same step");
        }

        [Test]
        public void Register_AnAgentThatThinksAlready_KeepsItsRhythm()
        {
            // The world's first agent takes phase 0: the one that joins next would get a later phase
            var world = new AIWorld();
            Agent(world);

            // Two agents think on the same beat; one joins the world between two thinks (its first ask for an attack
            // place does that) — it thinks again when the other does, not a phase later
            var alone = new GraphRig();
            var joins = new GraphRig();
            alone.SetGraph(BehaviorPresets.Sentry(new BehaviorPresets.Tuning()));
            joins.SetGraph(BehaviorPresets.Sentry(new BehaviorPresets.Tuning()));
            alone.Step(DT, 2);
            joins.Step(DT, 2);
            int before = joins.Agent.DecisionCount;
            Assert.Greater(before, 0, "Set-up: it has thought already");

            joins.Agent.SetWorld(world);
            for (int i = 0; i < 12; i++)
            {
                world.BeginStep();
                alone.Step(DT);
                joins.Step(DT);
                Assert.AreEqual(alone.Agent.DecisionCount, joins.Agent.DecisionCount, $"Step {i}: the same beat");
            }

            Assert.Greater(joins.Agent.DecisionCount, before, "Set-up: it thought again after joining");
        }

        [Test]
        public void Scheduler_NeverStepsAgents()
        {
            var world = new AIWorld();
            GraphRig rig = Agent(world);

            for (int i = 0; i < 10; i++)
                world.BeginStep();

            Assert.AreEqual(0f, rig.Context.LocalTime, "Only the agent's driver steps it");
            Assert.AreEqual(0, rig.Agent.DecisionCount);
        }

        [Test]
        public void Step_RegisteredAgent_AdvancesOncePerStep()
        {
            var world = new AIWorld();
            GraphRig rig = Agent(world);

            for (int i = 0; i < 50; i++)
            {
                world.BeginStep();
                rig.Step(DT);
            }

            Assert.AreEqual(50 * DT, rig.Context.LocalTime, 1e-4f);
        }

        [Test]
        public void Unregister_InsideAStep_DoesNotThrow()
        {
            var world = new AIWorld();
            GraphRig leaving = Agent(world);
            GraphRig staying = Agent(world);
            leaving.Agent.StateChanged += (_, _) => leaving.Agent.SetWorld(null);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    world.BeginStep();
                    leaving.Step(DT);
                    staying.Step(DT);
                }
            });

            Assert.IsFalse(world.Scheduler.Contains(leaving.Agent));
            Assert.IsTrue(world.Scheduler.Contains(staying.Agent));
        }

        [Test]
        public void SetWorld_GivesTheGlobalScope_AndLeavingTakesItBack()
        {
            var world = new AIWorld();
            GraphRig rig = Agent(world);
            world.Blackboard.Set("Alert", 1f);

            Assert.AreEqual(1f, rig.Context.GetVariable(BlackboardScope.Global, "Alert", 0f));

            rig.Agent.SetWorld(null);

            Assert.AreEqual(0f, rig.Context.GetVariable(BlackboardScope.Global, "Alert", 0f),
                "Without a world the global scope is the agent's own");
            Assert.AreEqual(0, world.Scheduler.Count);
        }

        /// <summary>Steps a crowd under a cap; each line lists the agents that decided on that step.</summary>
        private static List<string> Crowd(int agents, int cap, int steps, out List<GraphRig> rigs)
        {
            var world = new AIWorld();
            world.Scheduler.MaxDecisionsPerStep = cap;
            rigs = new List<GraphRig>();
            for (int i = 0; i < agents; i++)
                rigs.Add(Agent(world));

            var log = new List<string>();
            var counts = new int[agents];
            for (int step = 0; step < steps; step++)
            {
                world.BeginStep();
                string line = string.Empty;
                for (int i = 0; i < agents; i++)
                {
                    rigs[i].Step(DT);
                    if (rigs[i].Agent.DecisionCount == counts[i])
                        continue;

                    counts[i] = rigs[i].Agent.DecisionCount;
                    line += i + ",";
                }

                log.Add(line);
            }

            return log;
        }

        private static GraphRig Agent(AIWorld world)
        {
            var rig = new GraphRig();
            rig.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning()));
            rig.CreateTargetAt(new Vector3(3f, 0f, 0f));
            rig.Agent.SetWorld(world);              // after the graph: SetGraph resets the first decision
            return rig;
        }
    }
}
