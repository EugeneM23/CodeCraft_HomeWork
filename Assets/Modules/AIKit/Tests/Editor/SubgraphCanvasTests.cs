using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// A part of the graph collapsed into one node (the user's word, 15.11.2026; Unreal's Collapse Nodes): on the graph one
    /// node with the pins of the wires that cross its edge, inside a canvas of its own with only its nodes and the In / Out
    /// of its edge; only a look of the editor — the graph that runs is the same; kept in the asset; frames come back too.
    /// </summary>
    public sealed class SubgraphCanvasTests
    {
        private const string BRAIN = "aikit.task.combat-brain";
        private const string ROUTINE = "aikit.reasoner.daily-routine";
        private const string WAIT = "aikit.task.wait";
        private const string LOG = "aikit.task.debug-log";

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;
        private AgentState _brain;
        private AgentState _routine;
        private AgentState _wait;
        private AgentState _log;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            LogAssert.ignoreFailingMessages = true;
            _brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            _routine = _document.AddStateWithTask(null, ROUTINE, new Vector2(400f, 0f));
            _wait = _document.AddStateWithTask(null, WAIT, new Vector2(800f, 0f));
            _log = _document.AddStateWithTask(null, LOG, new Vector2(1200f, 0f));
            _document.Connect(_brain, _routine);
            _document.Connect(_routine, _wait);
            _document.Connect(_wait, _log, TransitionTrigger.TasksSucceeded);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        [Test]
        public void Collapse_TheNodesLeaveTheGraphsView_ItsNodeStandsWhereTheyStood_NeverTheBrain()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _wait.Id, _log.Id, _brain.Id }, "Combo");

            Assert.IsNotNull(part);
            CollectionAssert.AreEquivalent(new[] { _wait.Id, _log.Id }, part.Members, "The Brain is never collapsed");
            Assert.AreEqual(new Vector2(1000f, 0f), part.Position, "Where its nodes stood, on average");
            Assert.AreSame(part, _document.SubgraphOf(_log.Id));

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.IsNull(NodeTitled(canvas, "Wait"), "Behind the subgraph's node");
            Assert.IsNotNull(NodeTitled(canvas, "Daily Routine"));
            Node node = canvas.SubgraphNodeOf(part);
            Assert.IsNotNull(node);
            Port into = node.inputContainer.Query<Port>().ToList().Single(port => port.portName == "In");
            Assert.AreEqual(1, into.connections.Count(), "The decision's Do leads into the subgraph");
        }

        [Test]
        public void Open_OnlyItsNodes_AndTheInOfItsEdge_WiredToThem_EscBack()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _wait.Id, _log.Id }, "Combo");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            canvas.OpenSubgraph(part);

            Assert.AreSame(part, canvas.Scope);
            Assert.IsNotNull(NodeTitled(canvas, "Wait"));
            Assert.IsNotNull(NodeTitled(canvas, "Debug Log"));
            Assert.IsNull(NodeTitled(canvas, "Daily Routine"), "Not of the subgraph");
            Assert.IsNull(NodeTitled(canvas, "Brain"));
            Node enter = NodeTitled(canvas, "In");
            Assert.IsNotNull(enter, "The In of its edge");
            Port from = enter.outputContainer.Query<Port>().ToList().Single();
            StringAssert.Contains("Daily Routine", from.portName, "Named after what leads in");
            Assert.AreEqual(1, from.connections.Count());
            List<Label> crumbs = canvas.Q<VisualElement>("subgraph-crumbs").Query<Label>().ToList();
            Assert.AreEqual("Combo", crumbs.Last().text, "The path at the top: the graph, then the subgraph");

            canvas.CloseSubgraph();
            Assert.IsNull(canvas.Scope);
            Assert.IsNotNull(canvas.SubgraphNodeOf(part));
        }

        [Test]
        public void ANodeMadeInside_JoinsIt_ANodeDeleted_LeavesIt_TheLastOneGone_TheSubgraphGoes()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _log.Id }, "Combo");
            _document.ActiveSubgraph = part;

            AgentState made = _document.AddStateWithTask(null, WAIT, new Vector2(1300f, 200f));
            CollectionAssert.Contains(part.Members, made.Id, "Made inside: of it");

            _document.ActiveSubgraph = null;
            _document.Remove(made);
            _document.Remove(_document.States().Single(state => state.Id == _log.Id));
            Assert.IsEmpty(_document.Subgraphs, "Nothing left in it: it is gone");
        }

        [Test]
        public void Expand_TheNodesStandOnTheGraphAgain_AndItIsKeptInTheAsset_TheRunningGraphTheSame()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _wait.Id, _log.Id }, "Combo");

            var reopened = new GraphDocument(_asset);
            Assert.AreEqual(1, reopened.Subgraphs.Count, "Kept in the asset");
            Assert.AreEqual("Combo", reopened.Subgraphs[0].Title);
            BehaviorGraph read = GraphSerializer.Read(_asset, new List<GraphIssue>());
            Assert.AreEqual(4, read.Root.Children.Count, "The graph that runs: the same nodes, flat");

            _document.ExpandSubgraph(_document.Subgraphs.Single(other => other.Id == part.Id));
            Assert.IsEmpty(_document.Subgraphs);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.IsNotNull(NodeTitled(canvas, "Wait"), "Back on the graph");
        }

        [Test]
        public void AVariableReadOnlyInside_GoesInsideWithItsReader_NotLeftOnTheGraph()
        {
            _document.DeclareVariable("Pause", VariableType.Float);
            GraphVariableNode pause = _document.AddVariableNodeAt("Pause", new Vector2(700f, 150f));
            var wait = (WaitTask)_wait.Tasks[0];
            _document.SetReader(pause, wait);
            _document.BindField(wait, typeof(WaitTask).GetField(nameof(WaitTask.Seconds)), "Pause");
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _wait.Id, _log.Id }, "Combo");
            var canvas = new GraphViewCanvas();

            canvas.Show(_document);
            Assert.IsNull(NodeTitled(canvas, "Pause"), "Read only by a node of the subgraph: not on the graph");

            canvas.OpenSubgraph(part);
            Assert.IsNotNull(NodeTitled(canvas, "Pause"), "Inside, with the node that reads it");
        }

        [Test]
        public void CollapseDecisionChains_EachDecisionWithItsChain_IntoASubgraphNamedAfterIt_OnlyTheBrainStaysOnTheGraph()
        {
            int made = _document.CollapseDecisionChains();

            Assert.AreEqual(1, made);
            GraphSubgraph part = _document.Subgraphs.Single();
            Assert.AreEqual("Daily Routine", part.Title);
            CollectionAssert.AreEquivalent(new[] { _routine.Id, _wait.Id, _log.Id }, part.Members, "The decision, its Do and on");
            Assert.IsNull(_document.SubgraphOf(_brain.Id), "The Brain stays, wired into the subgraph");
            Assert.AreEqual(0, _document.CollapseDecisionChains(), "Collapsed already: nothing more");
        }

        [Test]
        public void CollapseDecisionChains_AChainCollapsedBeforeWithoutItsDecision_TheDecisionJoinsIt()
        {
            GraphSubgraph before = _document.CollapseToSubgraph(new[] { _wait.Id, _log.Id }, "Daily Routine");

            Assert.AreEqual(1, _document.CollapseDecisionChains());

            GraphSubgraph part = _document.Subgraphs.Single();
            Assert.AreEqual(before.Id, part.Id, "No second subgraph");
            CollectionAssert.AreEquivalent(new[] { _routine.Id, _wait.Id, _log.Id }, part.Members);
        }

        [Test]
        public void ADecisionNode_HasNoMarginOnTop_AFrameMovingItNeverSlidesItDown()
        {
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node decision = NodeTitled(canvas, "Daily Routine");
            // A frame moves its nodes by their layout (margin included) and writes it back as their place: a margin on top
            // slid the node down by its size on every step of a drag (16.11.2026)
            Assert.AreEqual(0f, decision.style.marginTop.value.value, "Its priority square stands above it absolutely");
        }

        [Test]
        public void InPlay_InsideAnOpenSubgraph_TheRunningNodeGlows()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _routine.Id, _wait.Id, _log.Id }, "Daily Routine");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            canvas.OpenSubgraph(part);

            canvas.Highlight(new HashSet<int> { _brain.Id, _routine.Id, _wait.Id }, _wait.Id, 0.5f, null, 0f);

            Node wait = NodeTitled(canvas, "Wait");
            Assert.Greater(wait.style.borderTopWidth.value, 0f, "The running node glows inside its subgraph");
            Assert.Greater(NodeTitled(canvas, "Daily Routine").style.borderTopWidth.value, 0f, "Its decision too");
        }

        [Test]
        public void InPlay_OnTheGraph_TheBrainItsSubgraphAndTheWireBetweenGlow_TheSubgraphSaysWhatRuns()
        {
            GraphSubgraph part = _document.CollapseToSubgraph(new[] { _routine.Id, _wait.Id, _log.Id }, "Daily Routine");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            canvas.Highlight(new HashSet<int> { _routine.Id, _wait.Id }, _wait.Id, 0.5f, null, 0f);

            Assert.Greater(NodeTitled(canvas, "Brain").style.borderTopWidth.value, 0f, "The Brain, while the agent runs");
            Node node = canvas.SubgraphNodeOf(part);
            Assert.GreaterOrEqual(node.style.borderTopWidth.value, 3f, "The subgraph holding the running node");
            StringAssert.Contains("Wait", node.Query<Label>().ToList().Select(label => label.text).FirstOrDefault(
                text => text.StartsWith("▶")), "What runs inside it");
            Port into = node.inputContainer.Query<Port>().ToList().Single(port => port.portName == "In");
            Edge wire = into.connections.Single();
            Assert.AreEqual(3, wire.edgeControl.edgeWidth, "The Brain's wire into it glows");
        }

        [Test]
        public void AFrame_OfThePickedNodes_IsMadeAgain()
        {
            GraphFrame frame = _document.AddFrame("Comment", new[] { _wait.Id, _log.Id });

            Assert.IsNotNull(frame);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.IsTrue(canvas.Query<Group>().ToList().Any(group => group.title == "Comment"));
        }

        private static Node NodeTitled(GraphViewCanvas canvas, string title) =>
            canvas.Query<Node>().ToList().FirstOrDefault(node => node.title == title);
    }
}
