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
    /// What a node is called (step 90, C3 / C4; spec 4.2–4.3, 10.10): by default its type — never a C# class name; the user
    /// may give it a label on the canvas (Undo, kept with the graph, shown wherever the node is named, with its type under
    /// it); NameByType never touches a label; an empty label or the type's title — called by its type again.
    /// </summary>
    public sealed class NodeNamingTests
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

        // ---------- the type, never a class name ----------

        [Test]
        public void DisplayName_OfEveryRegistryType_IsItsTitle_NeverAClassName()
        {
            var wrong = new List<string>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (entry.Info.Category == AIKitNodeAttribute.CODE || entry.Type == typeof(AgentState))
                    continue;

                AgentNode node = AIKitNodeRegistry.Create(entry.Info.Id);
                string shown = node.DisplayName;
                if (shown == entry.Type.Name || shown != NodeTitles.LeafOf(entry.Info.Path))
                    wrong.Add($"{entry.Info.Id}: '{shown}'");
            }

            CollectionAssert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void DisplayName_AStateWithNoName_IsItsBlocksTitle_ALabelComesFirst()
        {
            var state = new AgentState();
            state.AddTask(new WaitTask(1f));

            Assert.AreEqual("Wait", state.DisplayName, "A state with no name is called by its block");
            state.Name = "Idle";
            Assert.AreEqual("Idle", state.DisplayName, "A name code gave it (tests tell nodes apart by it)");
            state.Label = "Catch Breath";
            Assert.AreEqual("Catch Breath", state.DisplayName, "The user's label before everything");
            Assert.AreEqual("Catch Breath", NodeTitles.Shown(state));
            Assert.AreEqual("Wait", NodeTitles.Of(state), "The type stays the type");
        }

        [Test]
        public void Traced_TheTraceNamesANode_ByItsNameItsIdAndItsType()
        {
            var state = new AgentState { Id = 12 };
            state.AddTask(new WaitTask(1f));

            Assert.AreEqual("Wait #12", NodeTitles.Traced(state));
            state.Label = "Catch Breath";
            Assert.AreEqual("Catch Breath #12 (Wait)", NodeTitles.Traced(state), "A label never hides what the node is");
            Assert.AreEqual("nowhere", NodeTitles.Traced(null));
        }

        // ---------- the label ----------

        [Test]
        public void Rename_KeptWithTheGraph_ThroughReopeningAndTheSerializer()
        {
            AgentState waiting = _document.AddStateWithTask(null, WAIT, Vector2.zero);

            Assert.IsTrue(_document.RenameNode(waiting, "  Catch Breath  "));

            Assert.AreEqual("Catch Breath", waiting.Label, "Trimmed");
            Assert.AreEqual("Catch Breath", new GraphDocument(_asset).States().First(state => state.Tasks.Count > 0).Label,
                "Opened again: the label is there");
            AgentState read = GraphSerializer.Read(_asset, new List<GraphIssue>()).Root.Children.Single();
            Assert.AreEqual("Catch Breath", read.Label, "Read from the asset");
            Assert.AreEqual("Catch Breath", read.DisplayName, "And named by it at runtime (trace, not from, the head)");
            Assert.AreEqual("Wait", read.Name, "The service name stays the type");
        }

        [Test]
        public void Rename_EmptyOrTheTypesTitle_CalledByItsTypeAgain()
        {
            AgentState waiting = _document.AddStateWithTask(null, WAIT, Vector2.zero);
            _document.RenameNode(waiting, "Catch Breath");

            _document.RenameNode(waiting, "Wait");
            Assert.AreEqual("", waiting.Label, "The type's own title is no label");

            _document.RenameNode(waiting, "Catch Breath");
            Assert.IsTrue(_document.NameByType(waiting));
            Assert.AreEqual("", waiting.Label, "Name By Type");
            Assert.IsFalse(_document.RenameNode(waiting, "   "), "Nothing to change");
        }

        [Test]
        public void NameByType_ServiceNamesFollowTheType_LabelsStay()
        {
            var root = new AgentState("Root");
            var waiting = new AgentState("Draw The Sword") { Label = "Draw Sword" };
            waiting.AddTask(new WaitTask(0.5f));
            root.AddChild(waiting);
            var graph = new BehaviorGraph(root);

            Assert.IsTrue(NodeTitles.NameByType(graph));
            Assert.AreEqual("Wait", waiting.Name, "An own name of the old rule is the type now");
            Assert.AreEqual("Draw Sword", waiting.Label, "The user's label is left alone");
        }

        [Test]
        public void Canvas_ALabelledNode_ShowsTheLabel_AndItsTypeUnderIt()
        {
            AgentState waiting = _document.AddStateWithTask(null, WAIT, Vector2.zero);
            AgentState other = _document.AddStateWithTask(null, WAIT, new Vector2(0f, 300f));
            _document.RenameNode(waiting, "Catch Breath");

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node labelled = canvas.Query<Node>().ToList().Single(node => node.title == "Catch Breath");
            Label caption = labelled.Q<Label>(GraphViewCanvas.TYPE_CAPTION);
            Assert.IsNotNull(caption, "What it is stays seen");
            Assert.AreEqual("Wait", caption.text);
            Node plain = canvas.Query<Node>().ToList().Single(node => node.title == "Wait");
            Assert.IsNull(plain.Q<Label>(GraphViewCanvas.TYPE_CAPTION), "A node called by its type needs no caption");
            Assert.AreNotSame(labelled, plain);
            Assert.AreEqual("", other.Label);
        }

        [Test]
        public void Canvas_Rename_TheTitleBecomesAField_EnterKeeps_EscDrops()
        {
            AgentState waiting = _document.AddStateWithTask(null, WAIT, Vector2.zero);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            GraphViewCanvas.NodeRenamer renaming = canvas.BeginRename(waiting);
            Assert.IsNotNull(renaming);
            Assert.AreEqual("Wait", renaming.Field.value, "The field starts with what the node is called");
            renaming.Field.value = "Catch Breath";
            renaming.Cancel();
            Assert.AreEqual("", waiting.Label, "Esc: nothing changes");

            canvas.Show(_document);
            renaming = canvas.BeginRename(waiting);
            renaming.Field.value = "Catch Breath";
            renaming.Commit();
            Assert.AreEqual("Catch Breath", waiting.Label, "Enter: the label is kept");
        }

        [Test]
        public void Canvas_ALinkToALabelledNode_NamesItByTheLabel()
        {
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(400f, 0f));
            AgentState first = _document.AddStateWithTask(null, WAIT, new Vector2(800f, 0f));
            AgentState second = _document.AddStateWithTask(null, WAIT, new Vector2(1200f, 0f));
            _document.Connect(brain, routine);
            _document.Connect(routine, first);
            _document.Connect(first, second, TransitionTrigger.TasksSucceeded);
            _document.Connect(second, first, TransitionTrigger.TasksSucceeded);      // back: a link at its pin
            _document.RenameNode(first, "Catch Breath");

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Assert.IsTrue(canvas.Query<Node>().ToList().Any(node => node.title == "→ Catch Breath"),
                "The label shows on the link that leads to the node");
        }
    }
}
