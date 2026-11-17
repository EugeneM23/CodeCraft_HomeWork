using System.Collections.Generic;
using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// The choice a chain makes by itself: Random picks one of its numbered outputs with the agent's own random — the
    /// seed decides, so a run replays — and by their weights. What the Brain decides is BrainDecisionTests'; the
    /// picks of a running chain are RandomNodeTests'.
    /// </summary>
    public class DecisionNodeTests
    {
        private const int PICKS = 20;

        [Test]
        public void Random_TheSameSeed_TheSamePicks_WeightZeroNever()
        {
            var picks = new List<int>();
            for (int run = 0; run < 2; run++)
            {
                var rig = new GraphRig(seed: 42, trace: false);
                var random = new PickRandomTask
                {
                    Outputs = new List<RandomOutput>
                    {
                        new RandomOutput { Weight = 0f }, new RandomOutput { Weight = 1f }, new RandomOutput { Weight = 1f }
                    }
                };
                AgentState node = new AgentState("Random").AddTask(random);
                for (int output = 0; output < random.Outputs.Count; output++)
                {
                    node.AddTransition(new Transition
                    {
                        Target = new AgentState($"Output {output}"), Trigger = TransitionTrigger.TasksFinished, Output = output
                    });
                }

                for (int i = 0; i < PICKS; i++)
                    picks.Add(random.Pick(rig.Context, node));
            }

            CollectionAssert.AreEqual(picks.GetRange(0, PICKS), picks.GetRange(PICKS, PICKS), "The seed decides");
            CollectionAssert.DoesNotContain(picks, 0, "Weight zero is never picked");
            CollectionAssert.Contains(picks, 1);
            CollectionAssert.Contains(picks, 2);
        }
    }
}
