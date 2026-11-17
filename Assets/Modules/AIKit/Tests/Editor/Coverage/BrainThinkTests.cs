using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// What one think does with the blackboard and the On Tick pins (whole-plugin recheck, brain): a question asked
    /// twice in a think answers anew once the chain itself has written a variable between the two asks (the per-think
    /// answer is for the world's queries, handbook §3.2 p.6 — not for what the canvas just wrote); the Delta Time of a
    /// decision's On Tick keeps counting while the same decision is taken again after its chain.
    /// </summary>
    public sealed class BrainThinkTests
    {
        private const float DT = 0.05f;

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        [Test]
        public void AgentCondition_OneQuestionOfTwoBranches_AnInstantSubtractBetween_TheSecondReadsTheNewValue()
        {
            const string AMMO = "Ammo";
            var hasAmmo = new CompareCondition(AMMO, NumberComparison.Greater, 0f);
            var g = new BrainGraphBuilder();
            g.Variable(AMMO, VariableType.Float);
            AgentState first = g.Block(new BranchTask(hasAmmo), "A");
            AgentState spend = g.Block(new SubtractNumberTask { Variable = AMMO, Value = 1f }, "Spend");
            AgentState second = g.Block(new BranchTask(hasAmmo), "B");
            g.Do(g.Always(), first);
            g.Then(first, spend);
            g.Then(spend, second);
            g.Then(second, g.Block(new WaitTask(100f), "Fire"));
            g.OnFail(second, g.Block(new WaitTask(100f), "Reload"));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());
            rig.Context.Blackboard.Set(AMMO, 1f);
            var entered = new List<string>();
            rig.Instance.StateChanged += (_, to) => entered.Add(to?.DisplayName);

            rig.StepSeconds(0.5f, DT);

            Assert.AreEqual(0f, rig.Context.Blackboard.Get(AMMO, -1f), "The one round was spent");
            CollectionAssert.Contains(entered, "Reload", string.Join(" → ", entered));
            CollectionAssert.DoesNotContain(entered, "Fire", "B asked after the Subtract: no ammo, no fire");
        }

        [Test]
        public void AgentCondition_ALoopOfInstantBlocks_TheBranchSeesEachAdd_LeavesThroughTrueAtTheCount()
        {
            const string COUNT = "Count";
            const float ENOUGH = 3f;
            var g = new BrainGraphBuilder();
            g.Variable(COUNT, VariableType.Float);
            AgentState add = g.Block(new AddNumberTask(COUNT), "Add");
            AgentState check = g.Block(new BranchTask(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, ENOUGH)),
                "Enough");
            g.Do(g.Always(), add);
            g.Then(add, check);
            g.OnFail(check, add);
            g.Then(check, g.Block(new WaitTask(100f), "Done"));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Done", 0.5f, DT), rig.StateName);
            Assert.AreEqual(ENOUGH, rig.Context.Blackboard.Get(COUNT, 0f), "Out through True the moment the count is there");
        }

        [Test]
        public void AgentCondition_AskedTwiceWithNoWriteBetween_IsStillEvaluatedOnceAThink()
        {
            var counting = new CountingCondition();
            var g = new BrainGraphBuilder();
            AgentState first = g.Block(new BranchTask(counting), "First");
            AgentState second = g.Block(new BranchTask(counting), "Second");
            g.Do(g.Always(), first);
            g.OnEnd(first, second);
            g.OnEnd(second, g.Block(new WaitTask(0.2f), "Pause"));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());
            int runs = 0;
            rig.Instance.StateChanged += (_, to) => runs += to == second ? 1 : 0;

            rig.StepSeconds(1f, DT);

            Assert.Greater(runs, 1);
            Assert.AreEqual(runs, counting.Evaluations, "Nothing written between the two asks: one answer per think");
        }

        [Test]
        public void BehaviorGraphInstance_DecisionOnTick_SameDecisionRetaken_DeltaTimeAddsUpToTheTimeInCharge()
        {
            const string ELAPSED = "Elapsed";
            var g = new BrainGraphBuilder();
            g.Variable(ELAPSED, VariableType.Float);
            AgentState routine = g.Always();
            g.Do(routine, g.Block(new WaitTask(0.2f), "Pause"));          // no arrow after: the chain ends, taken again
            g.OnTick(routine, g.Block(new AddNumberTask
            {
                Variable = ELAPSED, Value = BlackboardValue<float>.Variable(BehaviorGraphInstance.DELTA_TIME)
            }, "Count Time"));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(g.Build());
            float firstTaken = float.NaN;
            int retaken = 0;
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != routine)
                    return;

                retaken++;
                if (float.IsNaN(firstTaken))
                    firstTaken = rig.Context.LocalTime;
            };
            int left = 0;
            rig.Context.DecisionLeft += () => left++;

            rig.StepSeconds(2f, DT);

            Assert.Greater(retaken, 4, "Set-up: the chain ended and the decision was taken again and again");
            Assert.AreEqual(0, left, "The same decision all the while: it never left");
            float inCharge = rig.Context.LocalTime - firstTaken;
            Assert.GreaterOrEqual(rig.Context.Blackboard.Get(ELAPSED, 0f), inCharge - DT - 1e-3f,
                "Delta Time adds up to the time the decision was in charge (only its very first run gives 0)");
        }

        // A condition that counts its real evaluations
        private sealed class CountingCondition : AgentCondition
        {
            public int Evaluations;

            protected override bool Evaluate(AgentContext context)
            {
                Evaluations++;
                return true;
            }
        }
    }
}
