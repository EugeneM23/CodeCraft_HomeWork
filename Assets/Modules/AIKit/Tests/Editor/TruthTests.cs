using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// What the documents promise about the module, held by tests (invariants 2, 6 and 11 of AIKit §1.4): a step with
    /// the trace off allocates nothing, one seed gives one trace and another seed another, a condition only reads.
    /// </summary>
    public class TruthTests
    {
        private const float DT = 0.02f;

        // The reach of the test's attacks (an ability's own range — the presets' Tuning has none)
        private const float REACH = 1.6f;

        [Test]
        public void Step_TraceOff_AllocatesNothing()
        {
            GraphRig rig = Fight(seed: 5, trace: false);

            // Warm up: every node has been entered, every list has grown, every profiler marker exists
            Run(rig, 600, 0);

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            Assert.That(() => Run(rig, 600, 600), NoAllocation.Constraint,
                "A step of a fighting agent with the trace off must not allocate");
        }

        [Test]
        public void SameSeed_SameTrace()
        {
            List<DecisionRecord> first = TraceOf(Fight(seed: 77, trace: true));
            List<DecisionRecord> second = TraceOf(Fight(seed: 77, trace: true));

            Assert.Greater(first.Count, 10, "The fight must produce decisions to compare");
            CollectionAssert.AreEqual(first, second, "One seed must give one trace");
        }

        [Test]
        public void OtherSeed_OtherTrace()
        {
            List<DecisionRecord> first = TraceOf(Fight(seed: 1, trace: true));
            List<DecisionRecord> second = TraceOf(Fight(seed: 2, trace: true));

            CollectionAssert.AreNotEqual(first, second, "The seed must reach the decisions (the ability choice)");
        }

        [Test]
        public void Conditions_OnlyRead_TheBlackboard()
        {
            var world = new AIWorld();
            var rig = new GraphRig(world: world);

            // The chain loops through a Branch, so its conditions are asked again and again, every one of them: "IsAlarmed
            // is on, or Alert is 0.5 or more, or Noise is above 0.3 (with slack: it remembers its answer)" — none of the
            // variables exists, so the answer is False
            var alarm = new AnyCondition(
                new CompareCondition("IsAlarmed", VariableType.Bool),
                new CompareCondition("Alert", NumberComparison.GreaterOrEqual, 0.5f),
                new CompareCondition("Noise", NumberComparison.Greater, 0.3f, slack: 0.1f));
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState branch = g.Block(new BranchTask(alarm));
            AgentState alarmed = g.Block(new WaitTask(0.1f), "Alarmed");
            AgentState idle = g.Block(new WaitTask(0.1f), "Idle");
            g.Do(routine, branch);
            g.Then(branch, alarmed);                    // True
            g.OnFail(branch, idle);                     // False
            g.Then(alarmed, branch);
            g.Then(idle, branch);
            rig.SetGraph(g.Build());

            int changes = 0;
            rig.Context.Blackboard.Changed += _ => changes++;
            world.Blackboard.Changed += _ => changes++;

            rig.StepSeconds(2f);

            Assert.AreEqual("Idle", rig.StateName);
            Assert.AreEqual(0, rig.Context.Blackboard.Count, "A condition must not declare the variable it reads");
            Assert.AreEqual(0, world.Blackboard.Count, "A condition must not write to the world");
            Assert.AreEqual(0, changes);
        }

        /// <summary>A melee chaser with three equal attacks: the seed picks among them.</summary>
        private static GraphRig Fight(int seed, bool trace)
        {
            var rig = new GraphRig(seed, trace);
            var tuning = new BehaviorPresets.Tuning { Recovery = 0.1f };
            foreach (string attack in new[] { "Jab", "Hook", "Kick" })
            {
                rig.AddAbility(attack, duration: 0.2f, cooldown: 0.1f, maxRange: REACH);
                tuning.Abilities.Add(GraphRig.Named(attack));      // the preset's Use Any Ability chooses among them
            }
            rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            rig.CreateTargetAt(new Vector3(4f, 0f, 0f));
            return rig;
        }

        /// <summary>The target dances in and out of reach, so the agent chases, strikes and recovers in turn.</summary>
        private static void Run(GraphRig rig, int steps, int firstStep)
        {
            for (int i = 0; i < steps; i++)
            {
                float x = 3f * Mathf.Sin((firstStep + i) * DT * 1.3f);
                rig.MoveTargetTo(new Vector3(x, 0f, 0f));
                rig.Step(DT);
            }
        }

        private static List<DecisionRecord> TraceOf(GraphRig rig)
        {
            Run(rig, 400, 0);
            var records = new List<DecisionRecord>();
            DecisionTrace trace = rig.Context.Trace;
            for (int i = 0; i < trace.Count; i++)
                records.Add(trace[i]);

            return records;
        }
    }
}
