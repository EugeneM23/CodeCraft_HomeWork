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
    /// Link labels docked at their output pins (step 90, C2; spec 4.12, 10.10): the place of a docked label (stacked under
    /// the others of its pin); "Replace With Link" docks and drops the bend points; dragged off, a label is free, "Dock To
    /// Pin" docks it again; an arrow wired back by hand is a link at once; the builders' rule (LinkLoops); an older graph's
    /// labels dock when it is opened; the look of the arrows survives GraphSerializer.Write.
    /// </summary>
    public sealed class LinkStubTests
    {
        private const string WAIT = "aikit.task.wait";

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

        // ---------- where a docked label stands ----------

        [Test]
        public void LinkSpot_TheFirstLevelWithItsPin_EachNextUnderTheOneBefore()
        {
            const float HEIGHT = 18f;
            var pin = new Rect(300f, 100f, 16f, 24f);

            Vector2 first = GraphLayout.LinkSpot(pin, 0, HEIGHT);
            Vector2 second = GraphLayout.LinkSpot(pin, 1, HEIGHT);
            Vector2 third = GraphLayout.LinkSpot(pin, 2, HEIGHT);

            Assert.AreEqual(pin.xMax + GraphLayout.LINK_OFFSET, first.x, "Just right of the pin");
            Assert.AreEqual(pin.center.y, first.y + HEIGHT * 0.5f, 0.001f, "Level with it");
            Assert.AreEqual(first.x, second.x);
            Assert.AreEqual(HEIGHT + GraphLayout.LINK_GAP, second.y - first.y, 0.001f, "Stacked downward");
            Assert.AreEqual(second.y - first.y, third.y - second.y, 0.001f);
            Assert.Less(first.y + HEIGHT, pin.yMax + 24f, "The first never reaches the next pin's row");
        }

        // ---------- the document ----------

        [Test]
        public void SetLink_DocksTheArrow_AndDropsItsBendPoints()
        {
            (AgentState first, AgentState second) = Chain();
            Transition arrow = first.Transitions.Single();
            _document.AddWaypoint(arrow, 0, new Vector2(500f, 300f));

            _document.SetLink(arrow);

            Assert.IsTrue(_document.IsLink(arrow));
            Assert.IsTrue(_document.IsDocked(arrow), "At its pin");
            CollectionAssert.IsEmpty(_document.WaypointsOf(arrow), "The bend points led to the far node");
            GraphRecord record = GraphLinks.RecordOf(_asset, first, 0);
            Assert.IsTrue(record.IsLink);
            Assert.IsFalse(record.LinkFree);
            Assert.AreSame(second, arrow.Target);
        }

        [Test]
        public void MoveLink_TheLabelIsFreeWhereItWasPut_DockToPin_DocksItAgain()
        {
            (AgentState first, AgentState _) = Chain();
            Transition arrow = first.Transitions.Single();
            _document.SetLink(arrow);

            _document.MoveLink(arrow, new Vector2(640f, 90f));

            Assert.IsFalse(_document.IsDocked(arrow), "Dragged off its pin");
            var reopened = new GraphDocument(_asset);
            Transition read = reopened.States().First(state => state.Transitions.Count > 0).Transitions.Single();
            Assert.IsTrue(reopened.TryLinkOf(read, out Vector2 at));
            Assert.AreEqual(new Vector2(640f, 90f), at, "Kept where it was put");
            Assert.IsFalse(reopened.IsDocked(read));

            reopened.DockLink(read);
            var docked = new GraphDocument(_asset);
            Assert.IsTrue(docked.IsDocked(docked.States().First(state => state.Transitions.Count > 0).Transitions.Single()),
                "Dock To Pin: at its pin again, kept with the graph");
        }

        [Test]
        public void Connect_AnArrowWiredBackByHand_IsADockedLinkAtOnce_AForwardOneAWire()
        {
            AgentState left = _document.AddStateWithTask(null, WAIT, new Vector2(0f, 0f));
            AgentState right = _document.AddStateWithTask(null, WAIT, new Vector2(400f, 0f));
            AgentState below = _document.AddStateWithTask(null, WAIT, new Vector2(400f, 300f));

            Transition forward = _document.Connect(left, right, TransitionTrigger.TasksSucceeded);
            Transition back = _document.Connect(right, left, TransitionTrigger.TasksSucceeded);
            Transition level = _document.Connect(right, below, TransitionTrigger.TasksFailed);

            Assert.IsFalse(_document.IsLink(forward), "To the right: a wire");
            Assert.IsTrue(_document.IsDocked(back), "Back across the canvas: a link at its pin");
            Assert.IsTrue(_document.IsDocked(level), "Level with it: a link too");
        }

        [Test]
        public void Connect_TheBrainsWires_StayWires()
        {
            AgentState routine = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(0f, 0f));
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", new Vector2(400f, 0f));

            Transition wire = _document.Connect(brain, routine);

            Assert.IsFalse(_document.IsLink(wire), "A Brain's wire leaves its frame: never a link");
        }

        [Test]
        public void Document_AnOlderGraphsLinks_DockWhenItIsOpened()
        {
            (AgentState first, AgentState _) = Chain();
            GraphRecord record = GraphLinks.RecordOf(_asset, first, 0);
            record.IsLink = true;                       // a link of before step 90: where it was put, no LinkFree
            record.LinkPosition = new Vector2(1402f, -1.3f);

            var opened = new GraphDocument(_asset);

            Transition arrow = opened.States().First(state => state.Transitions.Count > 0).Transitions.Single();
            Assert.IsTrue(opened.IsDocked(arrow), "The user's C2: compact, at its pin");
        }

        // ---------- the builders' rule ----------

        [Test]
        public void LinkLoops_EveryArrowBackOrLevel_IsADockedLink_TheBrainsWiresAndForwardArrowsStay()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState[] chain = g.Chain(routine, new WaitTask(1f), new WaitTask(1f));
            g.Then(chain[1], chain[0]);                 // back to the start of the chain
            BehaviorGraph graph = g.Build();
            GraphSerializer.Write(graph, _asset);
            Assert.IsTrue(GraphLinks.RecordOf(_asset, chain[1], 0).IsLink,
                "A graph written from code is laid out: its arrow back is a link already");

            // A builder lays it out its own way, then asks for the rule
            GraphLinks.RecordOf(_asset, chain[1], 0).IsLink = false;
            Place(g.Brain, 0f);
            Place(routine, 400f);
            Place(chain[0], 800f);
            Place(chain[1], 1200f);

            int made = GraphLinks.LinkLoops(_asset, graph);

            Assert.AreEqual(1, made, "Only the arrow back");
            Assert.IsTrue(GraphLinks.RecordOf(_asset, chain[1], 0).IsLink);
            Assert.IsFalse(GraphLinks.RecordOf(_asset, chain[1], 0).LinkFree);
            Assert.IsFalse(GraphLinks.RecordOf(_asset, chain[0], 0).IsLink, "Forward: a wire");
            Assert.IsFalse(GraphLinks.RecordOf(_asset, g.Brain, 0).IsLink, "The Brain's wire");
            Assert.AreEqual(0, GraphLinks.LinkLoops(_asset, graph), "Run again: nothing more to do");
        }

        // ---------- the look survives a write from code ----------

        [Test]
        public void SerializerWrite_KeepsFoldsBendPointsLinksAndExits_ThoughTheArrowsGetNewRecords()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState[] chain = g.Chain(routine, new WaitTask(1f), new WaitTask(1f));
            BehaviorGraph graph = g.Build();
            GraphSerializer.Write(graph, _asset);
            GraphRecord bent = GraphLinks.RecordOf(_asset, routine, 0);
            bent.Waypoints = new List<Vector2> { new(300f, 40f) };
            GraphRecord linked = GraphLinks.RecordOf(_asset, chain[0], 0);
            linked.IsLink = true;
            linked.LinkFree = true;
            linked.LinkPosition = new Vector2(77f, 88f);
            GraphRecord exit = GraphLinks.RecordOf(_asset, g.Brain, 0);
            exit.ExitSide = FrameSide.Top;
            exit.ExitAlong = 0.25f;
            _asset.Find(chain[1].Id).Folded = true;
            _asset.Find(chain[1].Tasks[0].Id).Waypoints = new List<Vector2> { new(900f, 60f) };     // a data wire, bent

            g.Then(chain[1], g.Block(new WaitTask(2f)));        // the graph changes in code: every record is written anew
            GraphSerializer.Write(graph, _asset);

            Assert.AreEqual(new Vector2(300f, 40f), GraphLinks.RecordOf(_asset, routine, 0).Waypoints.Single());
            GraphRecord link = GraphLinks.RecordOf(_asset, chain[0], 0);
            Assert.IsTrue(link.IsLink && link.LinkFree);
            Assert.AreEqual(new Vector2(77f, 88f), link.LinkPosition);
            Assert.AreEqual(FrameSide.Top, GraphLinks.RecordOf(_asset, g.Brain, 0).ExitSide);
            Assert.AreEqual(0.25f, GraphLinks.RecordOf(_asset, g.Brain, 0).ExitAlong, 0.0001f);
            Assert.IsTrue(_asset.Find(chain[1].Id).Folded, "The fold, by the node's id");
            Assert.AreEqual(new Vector2(900f, 60f), _asset.Find(chain[1].Tasks[0].Id).Waypoints.Single(),
                "A bent data wire into a block, by the block's id");
        }

        // ---------- the canvas ----------

        [Test]
        public void Canvas_ADockedLink_IsACompactLabel_NamingTheNodeItLeadsTo()
        {
            (AgentState first, AgentState second) = Chain();
            _document.SetLink(first.Transitions.Single());

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node label = canvas.Query<Node>().ToList().Single(node => node.title == "→ Wait");
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, label.mainContainer.style.display.value,
                "No node chrome: one pill");
            Assert.IsNotNull(label.Q("link-pill"));
            Assert.AreEqual(1, label.Query<Port>().ToList().Single().connections.Count(), "The arrow ends in it");
            Assert.AreNotSame(first, second);
        }

        // Two waits, the first leading to the second (to its right)
        private (AgentState First, AgentState Second) Chain()
        {
            AgentState first = _document.AddStateWithTask(null, WAIT, new Vector2(0f, 0f));
            AgentState second = _document.AddStateWithTask(null, WAIT, new Vector2(400f, 0f));
            _document.Connect(first, second, TransitionTrigger.TasksSucceeded);
            return (first, second);
        }

        private void Place(AgentState state, float x) => _asset.Find(state.Id).Position = new Vector2(x, 0f);
    }
}
