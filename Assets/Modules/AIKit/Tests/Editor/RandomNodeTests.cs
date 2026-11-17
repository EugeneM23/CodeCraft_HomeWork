using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// Random in a decision's chain, on a real agent (handbook §20b.2, §20b.4): it picks one of its outputs by their
    /// weights, at once; an output marked "not twice in a row" never comes right after itself; an output with no wire,
    /// or with weight 0, is never picked; the number of an output survives the graph asset.
    /// </summary>
    public sealed class RandomNodeTests
    {
        private const float STEP = 0.1f;
        private const float BLOCK = 0.2f;

        private GraphRig _rig;
        private BrainGraphBuilder _graph;
        private AgentState _random;
        private PickRandomTask _pick;
        private readonly List<string> _entered = new();

        // Brain → Patrol → Do → Random; each output of Random → a block, whose Done leads back into Random
        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = "Target";
            _graph.Variable("Target", VariableType.Transform);
            _pick = new PickRandomTask { Outputs = new List<RandomOutput>() };
            _random = _graph.Block(_pick, "Random");
            _graph.Do(_graph.Decision(new PatrolReasoner(), 1), _random);
            _entered.Clear();
        }

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        [Test]
        public void Random_PicksItsOutputsByTheirWeights_WeightZeroNever()
        {
            Output("Often", 3f);
            Output("Seldom", 1f);
            Output("Never", 0f);
            Run();

            _rig.StepSeconds(120f, STEP);

            int often = Count("Often");
            int seldom = Count("Seldom");
            Assert.Greater(often + seldom, 200, "Every block takes a moment, then Random rolls again");
            Assert.AreEqual(0, Count("Never"), "Weight 0 is never picked");
            Assert.AreEqual(3f, often / (float)seldom, 0.6f, "Weights 3 : 1 — picked about three times as often");
        }

        [Test]
        public void Random_NotTwiceInARow_NeverComesRightAfterItself()
        {
            Output("Dash", 10f, notTwice: true);
            Output("Step", 1f);
            Run();

            _rig.StepSeconds(60f, STEP);

            List<string> blocks = Blocks();
            Assert.Greater(blocks.Count, 100);
            for (int i = 1; i < blocks.Count; i++)
                Assert.IsFalse(blocks[i] == "Dash" && blocks[i - 1] == "Dash", $"Dash twice in a row at {i}");
            Assert.Greater(blocks.Count(name => name == "Dash"), blocks.Count / 3, "Still picked whenever it may be");
        }

        [Test]
        public void Random_NotTwiceInARow_TheOnlyOutput_IsPickedAgain()
        {
            Output("Dash", 1f, notTwice: true);
            Run();

            _rig.StepSeconds(5f, STEP);

            Assert.Greater(Count("Dash"), 5, "With nothing else to pick, the flow does not stop on the rule");
        }

        [Test]
        public void Random_AnOutputWithNoWire_IsNeverPicked()
        {
            Output("Wired", 1f);
            _pick.Outputs.Add(new RandomOutput { Weight = 100f });   // an output nobody wired
            Run();

            _rig.StepSeconds(10f, STEP);

            Assert.Greater(Count("Wired"), 20, "The wired output is picked every time: the empty one does not take turns");
        }

        [Test]
        public void Random_TakesNoTimeOfItsOwn_TheBlockStartsInTheSameThink()
        {
            _rig.ThinkInterval = 0.25f;
            Output("Only", 1f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Only", 2f, 0.05f));
            for (int i = 0; i < 100; i++)
            {
                _rig.Step(0.05f);
                Assert.AreNotEqual("Random", _rig.StateName, "The agent never stands a think in Random");
            }

            Assert.Greater(Count("Only"), 5, "Block after block, all the way");
        }

        [Test]
        public void Random_TheNumberOfAnOutput_SurvivesTheGraphAsset()
        {
            Output("First", 1f);
            Output("Second", 2f, notTwice: true);
            BehaviorGraph graph = _graph.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(graph, asset);
                BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());
                var states = new List<AgentState>();
                read.CollectStates(states);
                AgentState random = states.Single(state => state.Random != null);

                Assert.AreEqual(2, random.Random.Outputs.Count);
                Assert.AreEqual(2f, random.Random.Outputs[1].Weight);
                Assert.IsTrue(random.Random.Outputs[1].NotTwiceInARow);
                Assert.AreEqual("Second", random.Transitions.Single(edge => edge.Output == 1).Target.DisplayName);
                Assert.AreEqual("First", random.Transitions.Single(edge => edge.Output == 0).Target.DisplayName);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Canvas_ANewWireFromAnOutput_ReplacesItsOldOne_ATakenOffOutputMovesTheOthersUp()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                var document = new AIKit.Editor.GraphDocument(asset);
                AgentState random = document.AddStateWithTask(null, "aikit.task.random", Vector2.zero);
                AgentState a = document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
                AgentState b = document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
                AgentState c = document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
                document.AddRandomOutput(random.Random);
                Assert.AreEqual(3, random.Random.Outputs.Count, "Two outputs to start with, and one more");

                document.ConnectOutput(random, a, 0);
                document.ConnectOutput(random, b, 0);
                document.ConnectOutput(random, c, 2);

                Assert.AreSame(b, random.Transitions.Single(edge => edge.Output == 0).Target, "One wire per output");

                random.Random.Outputs[2].Weight = 5f;
                document.RemoveRandomOutput(random, 0);

                Assert.AreEqual(2, random.Random.Outputs.Count);
                Assert.IsFalse(random.Transitions.Exists(edge => edge.Target == b), "The output went with its wire");
                Assert.AreSame(c, random.Transitions.Single(edge => edge.Output == 1).Target,
                    "Output 3 is now output 2, with its wire");
                Assert.AreEqual(5f, random.Random.Outputs[1].Weight, "…and with its weight");

                // On the canvas: a pin per output, the wires leave from their own outputs, no "Done" pin, a "+" button
                document.ConnectOutput(random, a, 0);
                var canvas = new AIKit.Editor.GraphViewCanvas();
                canvas.Show(document);
                var node = canvas.Query<UnityEditor.Experimental.GraphView.Node>().ToList()
                    .Single(view => view.title.Contains("Random"));
                var wires = canvas.Query<UnityEditor.Experimental.GraphView.Edge>().ToList()
                    .Where(edge => edge.output?.node == node).ToList();

                Assert.AreEqual(2, wires.Count, "Both wires are drawn");
                Assert.AreNotSame(wires[0].output, wires[1].output, "Each leaves from the pin of its own output");
                Assert.IsFalse(node.Query<UnityEditor.Experimental.GraphView.Port>().ToList()
                    .Any(port => port.portName == "Done" || port.portName == "Success"), "Random has no Done pin");
                Assert.IsTrue(node.Query<Button>().ToList().Any(button => button.text == "+ output"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---------- the graph ----------

        // A block on the next output of Random, a moment long, and its wire back into Random
        private void Output(string name, float weight, bool notTwice = false)
        {
            int number = _pick.Outputs.Count;
            _pick.Outputs.Add(new RandomOutput { Weight = weight, NotTwiceInARow = notTwice });
            AgentState block = _graph.Block(new WaitTask(BLOCK), name);
            _graph.Out(_random, number, block);
            _graph.Then(block, _random);
        }

        private void Run()
        {
            _rig.SetGraph(_graph.Build());
            _rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add(to.DisplayName);
            };
        }

        private int Count(string name) => _entered.Count(entered => entered == name);

        // The blocks, in the order they ran (Random and the decisions left out)
        private List<string> Blocks() =>
            _entered.Where(name => name != "Random" && name != "Patrol" && name != "Brain").ToList();
    }
}
