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
    /// What the canvas draws (headless GraphViewCanvas): a problem of a shape of Detection is marked on the shape and "go
    /// to node" reaches it; a shape shows only the pins it reads (no Interval: Detection looks by its own); variable
    /// nodes are copied by their variable, so a paste into another graph duplicates nothing of it; an arrow with no pin
    /// of its own is drawn on a row of its own, and the canvas keeps listening after it.
    /// </summary>
    public sealed class CanvasViewTests
    {
        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            LogAssert.ignoreFailingMessages = true;     // half-made graphs: the validator names what is missing
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        private static List<Node> Nodes(GraphViewCanvas canvas) => canvas.Query<Node>().ToList();

        // ---------- a shape's problems are its node's ----------

        [Test]
        public void Canvas_AProblemOfADetectionShape_IsMarkedOnIt_AndFocusNodeReachesIt()
        {
            var shape = (RadiusSensorTask)_document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            shape.Radius = (BlackboardValue<float>)ValueFields.ToVariable(shape.Radius, "Sight");
            _document.Touch();
            Assert.IsTrue(_document.Issues.Exists(issue => issue.Code == GraphIssueCode.UnknownVariable &&
                                                          issue.RecordId == shape.Id), "Set-up: filed under the shape");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node node = Nodes(canvas).Single(found => found.title == "Radius");
            Assert.IsTrue(node.titleContainer.Query<Label>().ToList().Any(mark => mark.text == "!" && mark.tooltip.Contains("Sight")),
                "The '!' of the problem is on the shape");
            canvas.FocusNode(shape.Id);
            CollectionAssert.Contains(canvas.selection, node, "Go to node: the shape is picked");
        }

        // ---------- a shape shows the pins it reads ----------

        [Test]
        public void Canvas_ADetectionShape_HasNoIntervalPin()
        {
            _document.AddShape("aikit.sensor.cone", new Vector2(-400f, 0f));
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node cone = Nodes(canvas).Single(found => found.title == "Cone");
            CollectionAssert.AreEquivalent(new[] { "Range", "Half Angle", "Lose Range" },
                cone.inputContainer.Query<Port>().ToList().Select(port => port.portName),
                "What the shape reads — not Detection's interval, not its variable");
        }

        // ---------- variable nodes are copied by their variable ----------

        [Test]
        public void Canvas_PasteVariablesFromAnotherGraph_DuplicatesNothingThere()
        {
            _document.DeclareVariable("Enemy", VariableType.Transform);
            GraphVariableNode enemy = _document.AddVariableNodeAt("Enemy", new Vector2(100f, 100f));
            string copied = GraphViewCanvas.CopyData(_document, new[] { enemy });

            var other = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                var graphB = new GraphDocument(other);
                graphB.DeclareVariable("Home", VariableType.Transform);
                graphB.AddVariableNodeAt("Home", new Vector2(100f, 100f));

                CollectionAssert.IsEmpty(GraphViewCanvas.NodesToPaste(graphB, copied),
                    "B has no 'Enemy': nothing of B is duplicated instead");

                graphB.DeclareVariable("Enemy", VariableType.Transform);
                GraphVariableNode theirs = graphB.AddVariableNodeAt("Enemy", new Vector2(500f, 0f));
                CollectionAssert.AreEqual(new[] { theirs }, GraphViewCanvas.NodesToPaste(graphB, copied),
                    "A graph that has the variable gets one more node of it");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void Canvas_PasteVariablesAfterTheListChanged_DuplicatesTheCopiedVariable()
        {
            _document.DeclareVariable("Enemy", VariableType.Transform);
            _document.DeclareVariable("Home", VariableType.Transform);
            GraphVariableNode first = _document.AddVariableNodeAt("Home", new Vector2(0f, 0f));
            GraphVariableNode enemy = _document.AddVariableNodeAt("Enemy", new Vector2(100f, 100f));
            string copied = GraphViewCanvas.CopyData(_document, new[] { enemy });

            _document.RemoveVariableNode(first);            // the list changes between the copy and the paste

            List<GraphVariableNode> pasted = GraphViewCanvas.NodesToPaste(_document, copied);
            Assert.AreEqual(1, pasted.Count);
            Assert.AreEqual("Enemy", pasted[0].Variable, "The variable that was copied, wherever it stands in the list");
        }

        // ---------- an arrow with no pin of its own ----------

        [Test]
        public void Canvas_ARandomArrowPastItsOutputs_DoesNotBreakTheCanvas()
        {
            var g = new BrainGraphBuilder();
            AgentState decision = g.Always();
            AgentState random = g.Block(new PickRandomTask());
            AgentState wait = g.Block(new WaitTask(1f));
            g.Do(decision, random);
            g.Out(random, 2, wait);                         // Random has outputs 0 and 1 only
            GraphSerializer.Write(g.Build(), _asset);
            var document = new GraphDocument(_asset);
            AgentState waiting = document.States().Single(state => state.Tasks.FirstOrDefault() is WaitTask);
            var canvas = new GraphViewCanvas();

            Assert.DoesNotThrow(() => canvas.Show(document));

            Node randomNode = Nodes(canvas).Single(found => found.title == "Random");
            Assert.IsTrue(randomNode.Query<Port>().ToList().Any(port => port.direction == Direction.Output &&
                                                                      port.connections.Any()),
                "The arrow is drawn, from a pin of its own");
            AssertListens(canvas, document, waiting, Nodes(canvas).Single(found => found.title == "Wait"));
        }

        // The canvas still listens after a drawing: a node dragged reaches the document (the place the canvas has for it —
        // a canvas in no window lays nothing out, so that is not where the document had it)
        private static void AssertListens(GraphViewCanvas canvas, GraphDocument document, AgentState state, Node view)
        {
            var before = new Vector2(987f, 654f);
            document.Move(state, before);

            canvas.graphViewChanged(new GraphViewChange { movedElements = new List<GraphElement> { view } });

            Assert.AreNotEqual(before, document.PositionOf(state), "The canvas still listens: a drag reaches the document");
        }

        [Test]
        public void Canvas_ABrainWireToABlock_IsDrawnFromTheBrain_TheCanvasStillListens()
        {
            var g = new BrainGraphBuilder();
            AgentState wait = g.Block(new WaitTask(1f));
            g.Arrow(g.Brain, wait, TransitionTrigger.Always);       // a wire of the Brain into a block (no decision)
            GraphSerializer.Write(g.Build(), _asset);
            var document = new GraphDocument(_asset);
            var canvas = new GraphViewCanvas();

            Assert.DoesNotThrow(() => canvas.Show(document));

            Node brainNode = Nodes(canvas).Single(found => found.title == "Brain");
            Port decisions = brainNode.outputContainer.Query<Port>().ToList().Single(port => port.portName == "Decisions");
            Assert.AreEqual(1, decisions.connections.Count(), "The wire leaves the Brain's Decisions pin");
            AssertListens(canvas, document, document.States().Single(state => state.Tasks.FirstOrDefault() is WaitTask),
                Nodes(canvas).Single(found => found.title == "Wait"));
        }

        // ---------- what a node says of itself ----------

        private AgentState Brain(out AgentState decision, string decisionType = "aikit.reasoner.daily-routine")
        {
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            decision = _document.AddStateWithTask(null, decisionType, new Vector2(400f, 0f));
            _document.Connect(brain, decision);
            return brain;
        }

        [Test]
        public void Canvas_TheBrain_NoInPin_SaysItsRules()
        {
            Brain(out _);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node brain = Nodes(canvas).Single(found => found.title == "Brain");
            Port into = brain.inputContainer.Query<Port>().ToList().Single(port => port.portName == "In");
            Assert.AreEqual(DisplayStyle.None, into.style.display.value, "The graph starts in the Brain: nothing leads in");
            string rules = string.Format("keeps a decision ≥ {0:0.#} s (a reaction: at once) · never cuts a blow · " +
                                         "a chain that did nothing rests {1:0.#} s",
                BehaviorGraphInstance.MIN_DECISION_TIME, BehaviorGraphInstance.EMPTY_CHAIN_REST);
            Assert.AreEqual(rules, brain.Q<Label>("brain-rules").text, "Its rules said on the node, with its own numbers");
        }

        [Test]
        public void Canvas_ANodeNoChainReaches_IsFaded()
        {
            _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(0f, 400f));
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node wait = Nodes(canvas).Single(found => found.title == "Wait");
            Assert.AreEqual(0.45f, wait.style.opacity.value, 1e-4f, "It never runs: drawn faded");
            Assert.AreEqual("Never runs: the graph has no Brain", wait.tooltip);

            Brain(out _);
            canvas.Show(_document);
            wait = Nodes(canvas).Single(found => found.title == "Wait");
            Assert.AreEqual("Never runs: no decision's chain leads here", wait.tooltip);
            Node brain = Nodes(canvas).Single(found => found.title == "Brain");
            Assert.AreNotEqual(0.45f, brain.style.opacity.value, "The Brain is the start: never faded");
        }

        [Test]
        public void Canvas_ABlockInADecisionChainWithNoTarget_SaysTargetBrains()
        {
            Brain(out AgentState decision, "aikit.reasoner.chase");
            AgentState inChain = _document.AddStateWithTask(null, "aikit.task.chase", new Vector2(800f, 0f));
            AgentState loose = _document.AddStateWithTask(null, "aikit.task.chase", new Vector2(800f, 400f));
            _document.Connect(decision, inChain);
            _document.RenameNode(inChain, "In Chain");
            _document.RenameNode(loose, "Loose");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node chained = Nodes(canvas).Single(found => found.title == "In Chain");
            Assert.IsTrue(chained.Query<Label>().ToList().Any(label => label.text == "target: Brain's"),
                "Its target is the Brain's — said on the node");
            Assert.IsFalse(chained.inputContainer.Query<Port>().ToList().Any(port => port.portName == "Target"), "…no pin");
            Node alone = Nodes(canvas).Single(found => found.title == "Loose");
            Assert.IsTrue(alone.inputContainer.Query<Port>().ToList().Any(port => port.portName == "Target"),
                "A block in no chain of a decision takes its target by a wire");
        }

        [Test]
        public void Canvas_TwoDecisionsWithOnePriority_ArePaintedRed()
        {
            AgentState brain = Brain(out AgentState routine);
            AgentState patrol = _document.AddStateWithTask(null, "aikit.reasoner.patrol", new Vector2(400f, 300f));
            _document.Connect(brain, patrol);
            routine.Reasoner.Priority = 1;
            patrol.Reasoner.Priority = 1;
            _document.Touch();
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Label PriorityOf(string title) => Nodes(canvas).Single(found => found.title == title).Query<Label>().ToList()
                .Single(label => label.tooltip != null && label.tooltip.StartsWith("Priority"));

            var shared = new Color(0.9f, 0.3f, 0.3f);
            Assert.AreEqual(shared, PriorityOf("Daily Routine").style.backgroundColor.value, "Two of one priority: red");
            Assert.AreEqual(shared, PriorityOf("Patrol").style.backgroundColor.value);

            patrol.Reasoner.Priority = 2;
            _document.Touch();
            canvas.Show(_document);
            Assert.AreEqual(new Color(0.95f, 0.75f, 0.2f), PriorityOf("Daily Routine").style.backgroundColor.value,
                "A priority of its own: yellow");
        }

        [Test]
        public void Canvas_TwoLinksOfOnePin_StackDownward()
        {
            AgentState wait = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(400f, 0f));
            AgentState first = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            AgentState second = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(0f, 300f));
            Assert.IsTrue(_document.IsDocked(_document.Connect(wait, first, TransitionTrigger.TasksSucceeded)),
                "Set-up: back across the canvas — a link at its pin");
            Assert.IsTrue(_document.IsDocked(_document.Connect(wait, second, TransitionTrigger.TasksSucceeded)));
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            List<Node> labels = Nodes(canvas).Where(node => node.GetType().Name == "LinkNode").ToList();
            Assert.AreEqual(2, labels.Count);
            float[] xs = labels.Select(label => label.style.left.value.value).ToArray();
            float[] ys = labels.Select(label => label.style.top.value.value).OrderBy(y => y).ToArray();
            Assert.AreEqual(400f + 314f, xs[0], 0.01f, "Right of the pin, before the node is laid out");
            Assert.AreEqual(xs[0], xs[1], 0.01f, "One above the other");
            Assert.AreEqual(18f + GraphLayout.LINK_GAP, ys[1] - ys[0], 0.01f, "Stacked downward, a label's height apart");
        }

        // ---------- Play: what runs is lit ----------

        [Test]
        public void Canvas_Highlight_LeafTimeAndSensors()
        {
            AgentState brain = Brain(out AgentState routine);
            _document.DeclareVariable("Seen", VariableType.Transform);
            _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            _document.SetSensorVariable(_document.Detection(), "Seen");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Node brainNode = Nodes(canvas).Single(found => found.title == "Brain");
            Node routineNode = Nodes(canvas).Single(found => found.title == "Daily Routine");
            string Status(Node node) => node.titleContainer.Query<Label>().ToList()
                .Select(label => label.text).FirstOrDefault(text => text.StartsWith("▶")) ?? "";

            canvas.Highlight(new[] { brain.Id, routine.Id }, routine.Id, 1.25f, null, 0f);

            Assert.AreEqual($"▶ {12 / 10f:0.0} s", Status(routineNode), "The running node says how long it has run");
            Assert.AreEqual(3f, routineNode.style.borderTopWidth.value);
            Assert.AreEqual(3f, brainNode.style.borderTopWidth.value, "On the running path: lit too");
            Assert.AreEqual("", Status(brainNode), "…without a time of its own");

            canvas.Highlight(null, 0, 0f, null, 0f);
            Assert.AreEqual(0f, routineNode.style.borderTopWidth.value, "Nothing runs: idle again");
            Assert.AreEqual("", Status(routineNode));

            var player = new GameObject("Player") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var board = new AIKit.Blackboard();
                board.Set("Seen", player.transform);
                canvas.HighlightSensors(board);
                Node detection = Nodes(canvas).Single(found => found.title == "Detection");
                Assert.IsTrue(detection.titleContainer.Query<Label>().ToList().Any(label => label.text == "sees Player"));
                canvas.HighlightSensors(null);
                Assert.IsFalse(detection.titleContainer.Query<Label>().ToList().Any(label => label.text.StartsWith("sees")));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        // ---------- adding from the list, going to a node ----------

        [Test]
        public void Canvas_AddFromList_AndFocusNode()
        {
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            canvas.AddFromList("aikit.sensor.detection", new Vector2(10f, 10f));
            Assert.IsNull(_document.Detection(), "Detection is made with its first shape, never alone");
            var at = new Vector2(120f, 80f);
            canvas.AddFromList("aikit.task.wait", at);
            AgentState wait = _document.States().Single(state => state.Tasks.FirstOrDefault() is WaitTask);
            Assert.AreEqual(at, _document.PositionOf(wait), "Where the list was opened");

            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", new Vector2(400f, 0f));
            canvas.Show(_document);
            canvas.FocusNode(chase.Tasks[0].Id);
            Node picked = canvas.selection.OfType<Node>().Single();
            Assert.AreEqual("Chase", picked.title, "The problem of a block leads to its node");
        }
    }
}
