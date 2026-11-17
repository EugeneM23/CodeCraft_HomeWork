using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The variable nodes of the canvas (GraphDocument): a node a pin reads through stays the one node of its wire across
    /// every redraw — a shape of Detection's too; a node that is deleted takes its blocks' ids out of every wire; a pin that
    /// names a variable with no wire gets one wired node beside it, once; a sensor's own variable gets a node, a stale one
    /// goes; the node a sensor fills, a reader's one wire per variable, removing, duplicating and adding nodes.
    /// </summary>
    public sealed class CanvasVariableNodeTests
    {
        private const string SIGHT = "Sight";
        private const string TARGET = "Target";

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
            ElementInspector.Character = null;
        }

        private static FieldInfo RadiusField => typeof(RadiusSensorTask).GetField(nameof(RadiusSensorTask.Radius));

        private int NodesOf(string variable) => _document.VariableNodes.Count(node => node.Variable == variable);

        // ---------- a node a pin reads through is kept across redraws ----------

        [Test]
        public void GraphDocument_AShapeReadingAVariable_KeepsOneWiredNode_AcrossRedraws()
        {
            _document.DeclareVariable(SIGHT, VariableType.Float);
            SensorTask shape = _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            GraphVariableNode wired = _document.AddVariableNodeAt(SIGHT, new Vector2(-700f, 0f));
            _document.SetReader(wired, shape);
            _document.BindField(shape, RadiusField, SIGHT);

            for (int redraw = 0; redraw < 3; redraw++)
                _document.EnsureVariableNodes();         // what three drawings of the canvas do

            Assert.AreEqual(1, NodesOf(SIGHT), "No copy is made beside the shape: its wire is there");
            Assert.AreSame(wired, _document.WiredNode(shape, SIGHT), "The node the user wired stays wired");
            CollectionAssert.AreEqual(new[] { shape.Id }, wired.Readers);
        }

        [Test]
        public void GraphDocument_ARootSensorReadingAVariable_KeepsOneNode_AcrossRedraws()
        {
            _document.DeclareVariable(SIGHT, VariableType.Float);
            var sensor = new RadiusSensorTask { StoreIn = "", IsBackground = true };
            _document.Graph.Root.AddTask(sensor);
            _document.Touch();
            _document.BindField(sensor, RadiusField, SIGHT);

            for (int redraw = 0; redraw < 3; redraw++)
                _document.EnsureVariableNodes();

            Assert.AreEqual(1, NodesOf(SIGHT), "One node of the variable, made once");
            Assert.IsNotNull(_document.WiredNode(sensor, SIGHT), "…wired to the sensor that reads it");
        }

        // ---------- a node that is deleted ----------

        [Test]
        public void GraphDocument_RemoveANode_ItsBlocksIdsLeaveEveryVariableNode()
        {
            _document.DeclareVariable(TARGET, VariableType.Transform);
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", new Vector2(400f, 0f));
            _document.Connect(brain, chase);
            AgentTask block = chase.Tasks[0];
            GraphVariableNode node = _document.AddVariableNodeAt(TARGET, new Vector2(200f, 0f));
            _document.SetReader(node, block);
            int blockId = block.Id;
            Assert.AreNotEqual(0, blockId, "Set-up: the block has its id");

            _document.Remove(chase);

            CollectionAssert.DoesNotContain(node.Readers, blockId, "No variable node feeds a block that is gone");
            AgentState next = _document.AddStateWithTask(null, "aikit.task.chase", new Vector2(400f, 0f));
            Assert.IsNull(_document.WiredNode(next.Tasks[0], TARGET), "A new node never inherits a wire by its id");
        }

        // ---------- what a drawing makes sure of ----------

        [Test]
        public void GraphDocument_EnsureVariableNodes_ASensorsVariableGetsANode_AStaleOneGoes()
        {
            _document.DeclareVariable("Seen", VariableType.Transform);
            _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            DetectionTask detection = _document.Detection();
            _document.SetSensorVariable(detection, "Seen");
            _asset.VariableNodes.Add(new GraphVariableNode { Variable = "Gone" });     // declared nowhere, used nowhere

            _document.EnsureVariableNodes();

            Assert.AreEqual(0, NodesOf("Gone"), "A node of a variable that is gone goes");
            GraphVariableNode seen = _document.VariableNodes.Single(node => node.Variable == "Seen");
            Assert.AreEqual(_document.PositionOfNode(detection) + new Vector2(300f, 60f), seen.Position,
                "The variable Detection writes gets a node beside it");
        }

        [Test]
        public void GraphDocument_EnsureVariableNodes_ASensorIsNoReaderOfWhatItWrites()
        {
            _document.DeclareVariable("Seen", VariableType.Transform);
            _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            DetectionTask detection = _document.Detection();
            _document.SetSensorVariable(detection, "Seen");
            var made = new GraphVariableNode { Variable = "Seen", Readers = { detection.Id } };   // made the old way
            _asset.VariableNodes.Add(made);

            _document.EnsureVariableNodes();

            CollectionAssert.DoesNotContain(made.Readers, detection.Id, "Detection writes it: no reader's wire");
            Assert.AreEqual(1, NodesOf("Seen"), "The node stays, a plain node of the variable");
        }

        [Test]
        public void GraphDocument_EnsureVariableNodes_OnAReadOnlyGraph_TouchesNothing()
        {
            OldModelAssets.Write(_asset, OldModelAssets.REFLEX);
            _asset.VariableNodes.Add(new GraphVariableNode { Variable = "Gone" });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var document = new GraphDocument(_asset);
            Assert.IsTrue(document.IsReadOnly, "Set-up: a graph of the old model");
            string before = JsonUtility.ToJson(_asset);

            document.EnsureVariableNodes();

            Assert.AreEqual(before, JsonUtility.ToJson(_asset));
        }

        [Test]
        public void GraphDocument_EnsureVariableNodes_APinNamingAVariableWithoutAWire_GetsAWiredNodeBesideIt()
        {
            _document.DeclareVariable(TARGET, VariableType.Transform);
            var at = new Vector2(500f, 200f);
            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", at);
            var task = (ChaseTask)chase.Tasks[0];
            task.Target = TARGET;                           // named in code: no wire drawn
            _document.Touch();
            int pin = ValueFields.Pins(task).ToList().FindIndex(field => field.Name == nameof(ChaseTask.Target)) + 1;

            _document.EnsureVariableNodes();
            _document.EnsureVariableNodes();

            GraphVariableNode node = _document.VariableNodes.Single(found => found.Variable == TARGET);
            CollectionAssert.AreEqual(new[] { task.Id }, node.Readers, "Wired to the pin that names it");
            Assert.AreEqual(at + new Vector2(-190f, 4f + 22f * pin), node.Position, "Beside the pin");
        }

        // ---------- the node a sensor fills, a reader's wires ----------

        [Test]
        public void GraphDocument_SourceNode_TheFilledOne_ElseMarksTheFirst()
        {
            _document.DeclareVariable("Seen", VariableType.Transform);
            GraphVariableNode first = _document.AddVariableNodeAt("Seen", Vector2.zero);
            GraphVariableNode second = _document.AddVariableNodeAt("Seen", new Vector2(0f, 100f));

            Assert.AreSame(first, _document.SourceNode("Seen"), "None filled yet: the first is");
            Assert.IsTrue(first.Filled);

            _document.SetSource(second);
            Assert.IsTrue(second.Filled);
            Assert.IsFalse(first.Filled, "One node of a variable is filled");
            Assert.AreSame(second, _document.SourceNode("Seen"));
            Assert.IsNull(_document.SourceNode("Nope"));
        }

        [Test]
        public void GraphDocument_SetReader_OneWirePerVariablePerBlock()
        {
            _document.DeclareVariable(TARGET, VariableType.Transform);
            _document.DeclareVariable("Home", VariableType.Transform);
            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", Vector2.zero);
            AgentTask block = chase.Tasks[0];
            GraphVariableNode n1 = _document.AddVariableNodeAt(TARGET, new Vector2(-200f, 0f));
            GraphVariableNode n2 = _document.AddVariableNodeAt(TARGET, new Vector2(-200f, 100f));
            GraphVariableNode home = _document.AddVariableNodeAt("Home", new Vector2(-200f, 200f));

            _document.SetReader(n1, block);
            _document.SetReader(home, block);
            _document.SetReader(n2, block);

            CollectionAssert.DoesNotContain(n1.Readers, block.Id, "The new wire of the variable replaces the old one");
            Assert.AreSame(n2, _document.WiredNode(block, TARGET));
            Assert.AreSame(home, _document.WiredNode(block, "Home"), "Another variable keeps its own wire");
        }

        [Test]
        public void GraphDocument_RemoveVariableNode_UnwiresItsReaders_AnotherNodeTakesFilled()
        {
            _document.DeclareVariable("Pause", VariableType.Float);
            _document.SetDefault("Pause", 7.5f);
            AgentState wait = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            var task = (WaitTask)wait.Tasks[0];
            GraphVariableNode n1 = _document.AddVariableNodeAt("Pause", new Vector2(-200f, 0f));
            n1.Filled = true;
            _document.SetReader(n1, task);
            _document.BindField(task, typeof(WaitTask).GetField(nameof(WaitTask.Seconds)), "Pause");
            GraphVariableNode n2 = _document.AddVariableNodeAt("Pause", new Vector2(-200f, 100f));

            _document.RemoveVariableNode(n1);

            Assert.IsFalse(ValueFields.Uses(task.Seconds), "Its wire gone, the pin takes the variable no more");
            Assert.AreEqual(7.5f, (float)ValueFields.Constant(task.Seconds), "…and keeps the start value as its own number");
            Assert.IsTrue(n2.Filled, "Another node of the variable is the one filled now");
            Assert.IsTrue(_document.IsDeclared("Pause"), "The variable itself stays");
        }

        [Test]
        public void GraphDocument_DuplicateVariableNode_NoWires_40_50Away()
        {
            _document.DeclareVariable(TARGET, VariableType.Transform);
            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", Vector2.zero);
            GraphVariableNode original = _document.AddVariableNodeAt(TARGET, new Vector2(100f, 100f));
            _document.SetReader(original, chase.Tasks[0]);
            original.Filled = true;

            GraphVariableNode copy = _document.DuplicateVariableNode(original);

            Assert.AreEqual(TARGET, copy.Variable);
            Assert.AreEqual(new Vector2(140f, 150f), copy.Position);
            CollectionAssert.IsEmpty(copy.Readers, "A copy is a new place to wire from");
            Assert.IsFalse(copy.Filled);
            CollectionAssert.AreEqual(new[] { chase.Tasks[0].Id }, original.Readers, "The original keeps its wires");
        }

        [Test]
        public void GraphDocument_AddVariableNodeAt_UnknownNameAddsNothing_DeltaTimeIsKnown()
        {
            Assert.IsNull(_document.AddVariableNodeAt("Nope", Vector2.zero));
            CollectionAssert.IsEmpty(_document.VariableNodes);

            var at = new Vector2(30f, 40f);
            GraphVariableNode delta = _document.AddVariableNodeAt(BehaviorGraphInstance.DELTA_TIME, at);

            Assert.IsNotNull(delta, "Delta Time is always on the blackboard");
            Assert.AreEqual(at, delta.Position);
            Assert.AreSame(delta, _document.JustAdded, "It waits for a free place");
        }
    }
}
