using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// The canvas rules of 14.11.2026 (with step 90): a node is called by its type unless the user labels it (an older graph's own
    /// names go when it is opened); a new node lands on no other node; a new node's output pins that lead nowhere are
    /// folded into its card, and the fold is kept with the graph.
    /// </summary>
    public sealed class CanvasRulesTests
    {
        private BehaviorGraphAsset _asset;

        [SetUp]
        public void SetUp() => _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        // ---------- the type, not a name of its own ----------

        [Test]
        public void NodeTitles_ANodeIsCalledByItsType()
        {
            Assert.AreEqual("Wait", NodeTitles.Of(new WaitTask(1f)));
            Assert.AreEqual("Attack", NodeTitles.Of(new AttackTask()));
            Assert.AreEqual("Use Ability", NodeTitles.Of(new UseAbilityReasoner()));
            Assert.AreEqual("Charge", NodeTitles.Of(new ChargeTask()));

            var root = new AgentState("Root");
            var drawing = new AgentState("Draw The Sword");
            drawing.AddTask(new WaitTask(0.5f));
            root.AddChild(drawing);
            Assert.AreEqual("Wait", NodeTitles.Of(drawing), "A state is called by the task it holds");
            Assert.AreEqual("Root", NodeTitles.Of(root), "The root is no node of the canvas: it keeps its name");
        }

        [Test]
        public void Document_AnOlderGraphsOwnNames_AreItsTypesWhenOpened_TheUsersLabelsStay()
        {
            // Step 90 (C4): a node is called by its type unless the user gave it a label on the canvas. The service names
            // an older graph gave its nodes ("Draw The Sword" of the old rule) are its types when it is opened, in the asset
            // too — a label is the user's and is left alone
            var root = new AgentState("Root");
            AgentState drawing = Add(root, "Draw The Sword", new WaitTask(0.5f));
            AgentState special = Add(root, "Special Strike", new AttackTask { Move = "Heavy" });
            AgentState running = Add(root, "Run To Target", new ChaseTask());
            running.Label = "Run Him Down";
            drawing.AddTransition(new Transition { Target = special, Trigger = TransitionTrigger.TasksSucceeded });
            var graph = new BehaviorGraph(root);
            graph.Link();
            GraphSerializer.Write(graph, _asset);

            var document = new GraphDocument(_asset);

            CollectionAssert.AreEquivalent(new[] { "Wait", "Attack", "Chase" },
                document.Graph.Root.Children.Select(state => state.Name));
            CollectionAssert.AreEquivalent(new[] { "Wait", "Attack", "Run Him Down" },
                GraphSerializer.Read(_asset, new List<GraphIssue>()).Root.Children.Select(state => state.DisplayName),
                "The asset is fixed, not only the picture — the label kept");
            Assert.AreEqual("Heavy", ((AttackTask)document.Graph.Root.Children[1].Tasks[0]).Move,
                "What one Strike does differently stays in its settings");

            var canvas = new GraphViewCanvas();
            canvas.Show(document);
            List<string> titles = canvas.Query<Node>().ToList().Select(node => node.title).ToList();
            CollectionAssert.IsSupersetOf(titles, new[] { "Wait", "Attack", "Run Him Down" });
            Assert.IsFalse(titles.Any(title => title.Contains("Sword") || title.Contains("Special")));
        }

        [Test]
        public void Document_ANewNode_IsNamedAfterItsType_AndWaitsForAPlace_TillTheCanvasDrawsIt()
        {
            var document = new GraphDocument(_asset);

            AgentState strike = document.AddStateWithTask(null, "aikit.task.attack", new Vector2(100f, 50f));

            Assert.AreEqual("Attack", strike.DisplayName);
            Assert.AreSame(strike, document.JustAdded, "The canvas finds the new node a free place");
            new GraphViewCanvas().Show(document);
            Assert.IsNull(document.JustAdded, "…once: the next drawing does not move it again");
        }

        // ---------- no node lands on another ----------

        [Test]
        public void FreeSpot_ANewNodeOverOthers_SlidesDownPastThem_KeepingTheGap()
        {
            const float GAP = 24f;
            var wanted = new Rect(0f, 0f, 300f, 140f);
            var taken = new List<Rect>
            {
                new(-50f, -20f, 300f, 160f),        // under the new node
                new(100f, 160f, 300f, 100f),        // where the first slide would put it
                new(900f, 0f, 300f, 140f),          // far to the side: not in the way
                new(0f, 0f, 0f, 0f)                 // not laid out yet: not in the way
            };

            Vector2 free = GraphLayout.FreeSpot(wanted, taken, GAP);

            var placed = new Rect(free, wanted.size);
            Assert.AreEqual(wanted.x, free.x, "Straight down from where it was put");
            foreach (Rect other in taken.Where(rect => rect.width > 0f))
                Assert.IsFalse(GraphLayout.Overlaps(placed, other, GAP - 0.01f), $"Clear of {other}");
            Assert.AreEqual(160f + 100f + GAP, free.y, 0.01f, "Just below the lowest one in the way");
            Assert.AreEqual(new Vector2(0f, 400f),
                GraphLayout.FreeSpot(new Rect(0f, 400f, 300f, 140f), taken, GAP), "A free place stays as it is");
        }

        // ---------- the free output pins fold into the card ----------

        [Test]
        public void Document_ANewNode_HasItsFreeOutputPinsFoldedIntoTheCard_TheFoldIsKept()
        {
            var document = new GraphDocument(_asset);
            AgentState strike = document.AddStateWithTask(null, "aikit.task.attack", Vector2.zero);

            Assert.IsTrue(document.IsFolded(strike), "A new node starts folded");
            var canvas = new GraphViewCanvas();
            canvas.Show(document);
            Node card = canvas.Query<Node>().ToList().First(node => node.title == "Attack");
            List<Port> outputs = card.outputContainer.Query<Port>().ToList();
            Assert.Greater(outputs.Count, 1, "Set-up: Done, Out Of Reach, On Tick");
            Assert.IsTrue(outputs.All(port => port.style.display == DisplayStyle.None), "Leading nowhere: folded away");
            Label fold = card.outputContainer.Query<Label>().ToList().Last();
            Assert.AreEqual($"▸ {outputs.Count} more", fold.text, "The card says how many are folded in it");

            var reopened = new GraphDocument(_asset);
            Assert.IsTrue(reopened.IsFolded(reopened.Graph.Root.Children[0]), "Kept with the graph");

            document.SetFolded(strike, false);
            canvas.Show(document);
            card = canvas.Query<Node>().ToList().First(node => node.title == "Attack");
            Assert.IsTrue(card.outputContainer.Query<Port>().ToList().All(port => port.style.display != DisplayStyle.None),
                "Opened: every pin shows, to be wired");
        }

        [Test]
        public void Document_APinWithAWire_ShowsEvenFolded_AnOlderNodeIsNotFolded()
        {
            var root = new AgentState("Root");
            AgentState waiting = Add(root, "Wait", new WaitTask(1f));
            AgentState next = Add(root, "Wait", new WaitTask(1f));
            waiting.AddTransition(new Transition { Target = next, Trigger = TransitionTrigger.TasksSucceeded });
            var graph = new BehaviorGraph(root);
            graph.Link();
            GraphSerializer.Write(graph, _asset);
            var document = new GraphDocument(_asset);
            Assert.IsFalse(document.IsFolded(document.Graph.Root.Children[0]), "A node made before the rule is left as it was");

            document.SetFolded(document.Graph.Root.Children[0], true);
            var canvas = new GraphViewCanvas();
            canvas.Show(document);

            Node card = canvas.Query<Node>().ToList().First(node =>
                node.outputContainer.Query<Port>().ToList().Any(port => port.connected));
            List<Port> outputs = card.outputContainer.Query<Port>().ToList();
            Assert.IsTrue(outputs.Where(port => port.connected).All(port => port.style.display != DisplayStyle.None),
                "A wire needs its end: its pin shows");
            Assert.IsTrue(outputs.Where(port => !port.connected).All(port => port.style.display == DisplayStyle.None));
        }

        private static AgentState Add(AgentState root, string name, AgentTask task)
        {
            var state = new AgentState(name);
            state.AddTask(task);
            root.AddChild(state);
            return state;
        }
    }
}
