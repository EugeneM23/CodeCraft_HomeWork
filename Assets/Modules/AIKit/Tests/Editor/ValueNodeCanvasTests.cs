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
    /// The canvas side of the step-90 foundation (spec 4.8 / 4.9 / 10.10): a decision shows a Condition pin and an On Tick
    /// pin; a condition wired into a decision is saved; value nodes are drawn, wired into single and list pins, kept when
    /// nothing reads them, one may feed several pins; a block's requirement is written on its node.
    /// </summary>
    public sealed class ValueNodeCanvasTests
    {
        private const string BRAIN = "aikit.task.combat-brain";
        private const string ROUTINE = "aikit.reasoner.daily-routine";
        private const string WAIT = "aikit.task.wait";
        private const string COMPARE = "aikit.condition.compare";
        private const string READER = "aikit.tests.value-reader";
        private const string NUMBER = "aikit.tests.number-value";

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

        // ---------- a decision's pins ----------

        [Test]
        public void Canvas_ADecision_ShowsAConditionPin_AndAnOnTickPin()
        {
            AgentState brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, ROUTINE, new Vector2(400f, 0f));
            _document.Connect(brain, routine);

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node decision = NodeTitled(canvas, "Daily Routine");
            List<Port> inputs = decision.inputContainer.Query<Port>().ToList();
            List<Port> outputs = decision.outputContainer.Query<Port>().ToList();
            Assert.IsTrue(inputs.Any(port => port.portName == "Condition" && port.portType == typeof(bool)),
                "Only if: the decision's Condition pin");
            Assert.IsTrue(outputs.Any(port => port.portName == "On Tick"), "Its chain of instant blocks once per think");
            Assert.IsTrue(outputs.Any(port => port.portName == "Do"), "And its chain");
        }

        [Test]
        public void Document_AConditionAddedToADecision_IsWiredIntoItsPin_SavedAndDrawn()
        {
            AgentState brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, ROUTINE, new Vector2(400f, 0f));
            _document.Connect(brain, routine);

            AgentCondition asked = _document.AddConditionTo(routine.Reasoner, COMPARE, new Vector2(100f, -150f));
            AgentCondition second = _document.AddConditionTo(routine.Reasoner, COMPARE, new Vector2(100f, -250f));

            Assert.IsNotNull(asked);
            var reopened = new GraphDocument(_asset);
            ReasonerTask read = reopened.States().First(state => state.Reasoner != null).Reasoner;
            Assert.IsInstanceOf<AllCondition>(read.Condition, "A second condition makes an AND of the two");
            CollectionAssert.AreEquivalent(new[] { asked.Id, second.Id },
                ((AllCondition)read.Condition).Conditions.Select(condition => condition.Id));

            var canvas = new GraphViewCanvas();
            canvas.Show(reopened);
            Node decision = NodeTitled(canvas, "Daily Routine");
            Port pin = decision.inputContainer.Query<Port>().ToList().First(port => port.portName == "Condition");
            Assert.AreEqual(1, pin.connections.Count(), "The AND is wired into the decision's Condition pin");

            reopened.RemoveCondition(read.Condition);
            Assert.IsNull(new GraphDocument(_asset).States().First(state => state.Reasoner != null).Reasoner.Condition,
                "Taken away: the decision asks nothing more");
        }

        [Test]
        public void Document_AnOnTickChainOfADecision_IsSaved_AsATickArrow()
        {
            AgentState brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, ROUTINE, new Vector2(400f, 0f));
            AgentState set = _document.AddStateWithTask(null, "aikit.task.set-flag", new Vector2(800f, 120f));
            _document.Connect(brain, routine);

            _document.Connect(routine, set, TransitionTrigger.Tick);

            AgentState read = GraphSerializer.Read(_asset, new List<GraphIssue>()).Root.Children
                .First(state => state.Reasoner != null);
            Assert.IsTrue(read.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.Tick && arrow.Target?.Id == set.Id));
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Port tick = NodeTitled(canvas, "Daily Routine").outputContainer.Query<Port>().ToList()
                .First(port => port.portName == "On Tick");
            Assert.AreEqual(1, tick.connections.Count(), "Drawn from the decision's On Tick pin");
        }

        // ---------- value nodes ----------

        [Test]
        public void Document_AValueNodeWiredIntoABlock_IsSavedReadBackAndDrawnWired()
        {
            AgentState block = _document.AddStateWithTask(null, READER, new Vector2(300f, 0f));
            var reader = (TestValueReaderTask)block.Tasks[0];

            AgentValue value = _document.AddValueTo(reader, nameof(TestValueReaderTask.Amount), NUMBER, new Vector2(40f, 20f));
            ((TestNumberValue)value).Number = 7f;
            _document.Touch();

            var reopened = new GraphDocument(_asset);
            var read = (TestValueReaderTask)reopened.States().First(state => state.Tasks.FirstOrDefault() is TestValueReaderTask)
                .Tasks[0];
            Assert.IsInstanceOf<TestNumberValue>(read.Amount);
            Assert.AreEqual(7f, ((TestNumberValue)read.Amount).Number, "Its own setting is kept");
            Assert.AreEqual(new Vector2(40f, 20f), reopened.PositionOfNode(read.Amount), "Where it was put");

            var canvas = new GraphViewCanvas();
            canvas.Show(reopened);
            Node number = NodeTitled(canvas, "Number Value");
            Port output = number.titleContainer.Query<Port>().ToList().Single(port => port.direction == Direction.Output);
            Port amount = NodeTitled(canvas, "Value Reader").inputContainer.Query<Port>().ToList()
                .First(port => port.portName == "Amount");
            Assert.AreEqual(1, output.connections.Count(), "One wire out of the value node");
            Assert.AreSame(amount, output.connections.Single().input, "Into the block's Amount pin");
        }

        [Test]
        public void Document_AValueNodeWiredToNothing_IsKeptWithTheGraph_AndStaysOnTheCanvas()
        {
            AgentValue loose = _document.AddValue(NUMBER, new Vector2(-200f, 50f));

            var reopened = new GraphDocument(_asset);
            List<AgentValue> values = reopened.Values();
            Assert.AreEqual(1, values.Count, "Saved, though nothing reads it");
            Assert.AreEqual(loose.Id, values[0].Id);
            CollectionAssert.IsEmpty(reopened.ReadersOf(values[0]));

            var canvas = new GraphViewCanvas();
            canvas.Show(reopened);
            Assert.IsNotNull(NodeTitled(canvas, "Number Value"), "Drawn, to be wired");
        }

        [Test]
        public void Document_OneValueNode_FeedsTwoPins_AndPullingOneWire_KeepsTheOther()
        {
            AgentState first = _document.AddStateWithTask(null, READER, new Vector2(300f, 0f));
            AgentState second = _document.AddStateWithTask(null, READER, new Vector2(300f, 300f));
            AgentValue shared = _document.AddValue(NUMBER, new Vector2(-100f, 100f));
            var one = (TestValueReaderTask)first.Tasks[0];
            var two = (TestValueReaderTask)second.Tasks[0];

            Assert.IsTrue(_document.WireValue(shared, one, nameof(TestValueReaderTask.Amount)));
            Assert.IsTrue(_document.WireValue(shared, two, nameof(TestValueReaderTask.Amount)));

            Assert.AreEqual(2, _document.ReadersOf(shared).Count);
            var reopened = new GraphDocument(_asset);
            List<TestValueReaderTask> readers = reopened.States().SelectMany(state => state.Tasks)
                .OfType<TestValueReaderTask>().ToList();
            Assert.AreSame(readers[0].Amount, readers[1].Amount, "One node of data read by both, saved once");
            Assert.AreEqual(1, reopened.Values().Count);

            reopened.UnwireValue(readers[0].Amount, readers[0], nameof(TestValueReaderTask.Amount));
            Assert.IsNull(readers[0].Amount);
            Assert.IsNotNull(readers[1].Amount, "The other wire stays");
            reopened.UnwireValue(readers[1].Amount, readers[1], nameof(TestValueReaderTask.Amount));
            Assert.AreEqual(1, new GraphDocument(_asset).Values().Count, "Read by nobody, it stays on the canvas");
        }

        [Test]
        public void Canvas_AListPin_TakesSeveralValueNodes_OnePinManyWires()
        {
            AgentState block = _document.AddStateWithTask(null, READER, new Vector2(300f, 0f));
            var reader = (TestValueReaderTask)block.Tasks[0];
            _document.AddValueTo(reader, nameof(TestValueReaderTask.Numbers), NUMBER, new Vector2(0f, 0f));
            _document.AddValueTo(reader, nameof(TestValueReaderTask.Numbers), NUMBER, new Vector2(0f, 120f));

            Assert.AreEqual(2, reader.Numbers.Count);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Port numbers = NodeTitled(canvas, "Value Reader").inputContainer.Query<Port>().ToList()
                .Single(port => port.portName == "Numbers");
            Assert.AreEqual(Port.Capacity.Multi, numbers.capacity, "A list pin takes any number of wires");
            Assert.AreEqual(2, numbers.connections.Count());
        }

        [Test]
        public void Document_ARemovedValueNode_LeavesEveryPinItFed()
        {
            AgentState block = _document.AddStateWithTask(null, READER, new Vector2(300f, 0f));
            var reader = (TestValueReaderTask)block.Tasks[0];
            AgentValue value = _document.AddValueTo(reader, nameof(TestValueReaderTask.Amount), NUMBER, Vector2.zero);

            _document.Remove(value);

            Assert.IsNull(reader.Amount);
            CollectionAssert.IsEmpty(new GraphDocument(_asset).Values());
        }

        // ---------- what a block says of itself ----------

        [Test]
        public void Canvas_TheRequirementOfABlock_IsWrittenOnItsNode()
        {
            _document.AddStateWithTask(null, READER, Vector2.zero);
            _document.AddStateWithTask(null, WAIT, new Vector2(0f, 300f));

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Label line = NodeTitled(canvas, "Value Reader").Q<Label>(GraphViewCanvas.REQUIREMENT);
            Assert.IsNotNull(line, "The block says what it requires");
            Assert.AreEqual(TestValueReaderTask.REQUIRES, line.text);
            Assert.IsNull(NodeTitled(canvas, "Wait").Q<Label>(GraphViewCanvas.REQUIREMENT), "Wait says nothing");
        }

        // ---------- a block's output pins (step 91) ----------

        [Test]
        public void Canvas_TryGetComponent_ItsComponentAndFoundArePinsOfTheBlock_WiredIntoAGet_SavedAndDrawn()
        {
            AgentState block = _document.AddStateWithTask(null, "aikit.task.try-get-component", new Vector2(0f, 0f));
            var look = (TryGetComponentTask)block.Tasks[0];
            AgentValue get = _document.AddValue("aikit.value.get", new Vector2(400f, 0f));

            Assert.IsTrue(_document.WireValue(look.Component, get, nameof(GetValue.As)));

            var reopened = new GraphDocument(_asset);
            var readGet = (GetValue)reopened.Values().First(value => value is GetValue);
            var readLook = (TryGetComponentTask)reopened.States().First(state => state.Tasks.Count > 0 &&
                                                                               state.Tasks[0] is TryGetComponentTask).Tasks[0];
            Assert.AreSame(readLook.Component, readGet.As, "The Get reads the block's own Component");
            Assert.AreSame(readLook, readGet.As is IBlockOutput output ? output.Block : null);

            var canvas = new GraphViewCanvas();
            canvas.Show(reopened);
            Node node = NodeTitled(canvas, "Try Get Component");
            List<Port> outputs = node.outputContainer.Query<Port>().ToList();
            Port component = outputs.First(port => port.portName == "Component");
            Assert.IsTrue(outputs.Any(port => port.portName == "Found" && port.portType == typeof(AgentValue)), "Its flag");
            Assert.AreEqual(1, component.connections.Count(), "Drawn from the block's own pin into the Get");
            Assert.IsNull(canvas.Query<Node>().ToList().FirstOrDefault(other => other.title.Contains(": Component")),
                "The output is no node of its own");

            reopened.Remove(reopened.States().First(state => state.Tasks.Count > 0 && state.Tasks[0] is TryGetComponentTask));
            Assert.IsNull(((GetValue)new GraphDocument(_asset).Values().First(value => value is GetValue)).As,
                "The block is gone: the Get reads nothing of it (no wire to nowhere)");
        }

        private static Node NodeTitled(GraphViewCanvas canvas, string title) =>
            canvas.Query<Node>().ToList().FirstOrDefault(node => node.title == title);
    }
}
