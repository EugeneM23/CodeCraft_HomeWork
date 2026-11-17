using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AIKit.Tests
{
    /// <summary>
    /// A crowd of fighting agents for a fixed number of steps: prints milliseconds per agent-step, decisions apart from
    /// execution. The threshold is relative — a multiple of the baseline measured when stage 2 closed (README,
    /// "Production"), so a slower machine does not fail it but a regression of several times does.
    /// </summary>
    public class PerfSmokeTests
    {
        private const int AGENTS = 50;

        // The reach of the crowd's strike (an ability's own range — the presets' Tuning has none)
        private const float REACH = 1.6f;
        private const int STEPS = 2000;
        private const float DT = 0.02f;

        /// <summary>
        /// Microseconds per agent-step on the stage 2 machine (editor, Mono, Debug code): 0.3 for the Melee Chaser of
        /// then; × 1.84 since step 89, when the preset became the canvas's blocks (its Chase takes a place on the ring, so
        /// every target is an engagement the world steps) — measured old against new on one machine, runs alternating.
        /// </summary>
        private const double BASELINE_MICROSECONDS = 0.55;

        /// <summary>How many times slower than the baseline still passes.</summary>
        private const double TOLERANCE = 5.0;

        [Test, Explicit("PORTING: timing depends on the machine and editor load; run by hand")]
        public void Crowd_StepCost_StaysNearTheBaseline()
        {
            var world = new AIWorld();
            var rigs = new List<GraphRig>();
            for (int i = 0; i < AGENTS; i++)
            {
                var rig = new GraphRig(seed: 100 + i, trace: false);
                var tuning = new BehaviorPresets.Tuning();
                rig.AddAbility("Strike", duration: 0.3f, cooldown: 0.4f, maxRange: REACH);
                tuning.Abilities.Add(GraphRig.Named("Strike"));
                rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
                rig.CreateTargetAt(new Vector3(4f, 0f, 0f));
                rig.Agent.SetWorld(world);
                rigs.Add(rig);
            }

            Run(world, rigs, 100, 0);                       // warm-up
            double thinkBefore = world.Scheduler.ThinkMillisecondsTotal;
            var watch = Stopwatch.StartNew();
            Run(world, rigs, STEPS, 100);
            watch.Stop();

            double total = watch.Elapsed.TotalMilliseconds;
            double think = world.Scheduler.ThinkMillisecondsTotal - thinkBefore;
            double perAgentStep = total * 1000.0 / (AGENTS * STEPS);
            Debug.Log($"AIKit perf: {AGENTS} agents × {STEPS} steps — {total:0.0} ms total, think {think:0.0} ms, " +
                      $"tick+apply {total - think:0.0} ms, {perAgentStep:0.00} µs per agent-step " +
                      $"(baseline {BASELINE_MICROSECONDS:0.00})");

            Assert.Less(perAgentStep, BASELINE_MICROSECONDS * TOLERANCE,
                $"A step costs {perAgentStep:0.00} µs per agent, over {TOLERANCE}× the baseline");
        }

        private static void Run(AIWorld world, List<GraphRig> rigs, int steps, int firstStep)
        {
            for (int step = 0; step < steps; step++)
            {
                world.BeginStep();
                float x = 3f * Mathf.Sin((firstStep + step) * DT);
                for (int i = 0; i < rigs.Count; i++)
                {
                    rigs[i].MoveTargetTo(new Vector3(x + i * 0.01f, 0f, 0f));
                    rigs[i].Step(DT);
                }
            }
        }
    }
}
