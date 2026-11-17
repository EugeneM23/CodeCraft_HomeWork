using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// An On Tick wire runs only blocks that take no time (whole-plugin recheck, data / brain): a block on an On Tick pin
    /// that is not instant (a Take Attack Slot that waits, a Square Off) never runs — the graph says so on that block,
    /// the canvas fades it (BehaviorGraph.Runs), and it is not counted as an asker of attack places: the Attack Slot
    /// it was to answer is a "nobody asks" problem. The canvas is the program: a wire that does nothing is a problem.
    /// </summary>
    public sealed class DataOnTickWireTests
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
        public void GraphValidator_ANonInstantBlockOnAnOnTickPin_IsAProblem_AndNoAskerIsCounted()
        {
            BehaviorGraph graph = Waiting(new TakeAttackSlotTask { WaitTime = 0.3f }, out AgentState ask,
                out AttackSlotCondition mine);
            List<GraphIssue> issues = Validate(graph);

            List<GraphIssue> dead = issues.Where(issue => issue.RecordId == ask.Id && issue.Message.Contains("On Tick")).ToList();
            Assert.AreEqual(1, dead.Count, "The block on the On Tick pin is named, once: " + string.Join("\n", issues));
            Assert.IsFalse(dead[0].IsError, "A warning: the graph runs, that block never does");
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot && issue.RecordId == mine.Id),
                "Nobody asks for the place the Attack waits for");
            Assert.IsFalse(graph.Runs(ask), "It never runs: the canvas fades it");
        }

        [Test]
        public void GraphValidator_AnInstantAskOnAnOnTickPin_IsNoProblem_AndCountsAsTheAsker()
        {
            BehaviorGraph graph = Waiting(new TakeAttackSlotTask { WaitTime = 0f }, out AgentState ask, out _);
            List<GraphIssue> issues = Validate(graph);

            Assert.IsFalse(issues.Exists(issue => issue.RecordId == ask.Id), string.Join("\n", issues));
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot), string.Join("\n", issues));
            Assert.IsTrue(graph.Runs(ask));
        }

        [Test]
        public void GraphValidator_ASquareOffOnAnOnTickPin_NeverRuns_IsNoAsker_InTheChainItIs()
        {
            BehaviorGraph onTick = Waiting(new SquareOffTask(), out AgentState onPin, out _);
            Assert.IsTrue(Validate(onTick).Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot),
                "On an On Tick pin it never runs: nobody asks");
            Assert.IsFalse(onTick.Runs(onPin));

            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new FightReasoner { Closer = 100f, Condition = new AttackSlotCondition(AttackSlotIs.Mine) }, 1),
                g.Block(new WaitTask(100f), "Fight Block"));
            g.Do(g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 2),
                g.Block(new SquareOffTask(), "Square Off"));
            Assert.IsFalse(Validate(g.Build()).Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot),
                "In the chain behind Do it runs and asks the common group");
        }

        [Test]
        public void BehaviorGraphInstance_OnTickIntoABlockThatIsNotInstant_TheAskNeverRuns_TheAttackNeverComes()
        {
            var world = new AIWorld();
            var go = new GameObject(TARGET) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.AddComponent<AttackSlotCapacity>().Slots = 1;
            go.transform.position = new Vector3(3f, 0f, 0f);
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.SetGraph(Waiting(new TakeAttackSlotTask { WaitTime = 0.5f }, out _, out _));
            rig.Agent.Blackboard.Set(TARGET, go.transform);

            bool fought = false;
            for (float t = 0f; t < 2f; t += DT)
            {
                world.BeginStep(DT);
                rig.Step(DT);
                fought |= rig.StateName == "Fight Block";
            }

            rig.Agent.gameObject.SetActive(false);
            Assert.IsFalse(fought, "The ask never ran: the place never came (the graph says why — the test above)");
            Assert.AreEqual("Wait Block", rig.StateName);
        }

        // ---------- the graph ----------

        // Circle (an Attack Slot not mine) waits with this block on its On Tick pin; the Attack (an Attack Slot mine) fights
        private static BehaviorGraph Waiting(AgentTask onTick, out AgentState ask, out AttackSlotCondition mine)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            mine = new AttackSlotCondition(AttackSlotIs.Mine);
            g.Do(g.Decision(new FightReasoner { Closer = 100f, Condition = mine }, 1), g.Block(new WaitTask(100f), "Fight Block"));
            AgentState circle = g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 2);
            g.Do(circle, g.Block(new WaitTask(100f), "Wait Block"));
            ask = g.Block(onTick, "Ask");
            g.OnTick(circle, ask);
            return g.Build();
        }

        private List<GraphIssue> Validate(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return GraphValidator.Validate(asset);
        }
    }
}
