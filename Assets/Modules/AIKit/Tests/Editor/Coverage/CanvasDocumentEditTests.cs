using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The edits of the graph document: a start value changed on the Blackboard is an Undo step of its own; one more
    /// condition on a question that is an OR is ANDed with it (as every other way of joining does); a graph opened
    /// read-only takes no new sensor, shape or variable node, even in memory; every node an edit makes waits for a free
    /// place, the Branch made for a condition and the Detection made for a shape too.
    /// </summary>
    public sealed class CanvasDocumentEditTests
    {
        private const string COMPARE = "aikit.condition.compare";

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        private AgentState Routine(out AgentState brain)
        {
            brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(400f, 0f));
            _document.Connect(brain, routine);
            return routine;
        }

        // ---------- a start value is an Undo step of its own ----------

        [Test]
        public void GraphDocument_SetDefault_IsAnUndoStepOfItsOwn()
        {
            _document.DeclareVariable("Pause", VariableType.Float);
            Undo.IncrementCurrentGroup();
            _document.SetDefault("Pause", 7.5f);
            Undo.IncrementCurrentGroup();

            Undo.PerformUndo();
            _asset.MarkChanged();

            var undone = new GraphDocument(_asset);
            Assert.IsTrue(undone.IsDeclared("Pause"), "Only the start value is undone, not the declaration before it");
            Assert.AreEqual(0f, undone.DefaultOf("Pause"));
            Undo.PerformRedo();
            _asset.MarkChanged();
            Assert.AreEqual(7.5f, new GraphDocument(_asset).DefaultOf("Pause"));
        }

        // ---------- one more condition on an OR: an AND of the two ----------

        [Test]
        public void GraphDocument_AddConditionToADecisionWhoseConditionIsAnOr_AndsTheNewOne()
        {
            AgentState routine = Routine(out _);
            var or = (AnyCondition)_document.AddConditionTo(routine.Reasoner, "aikit.condition.any", new Vector2(100f, -200f));
            _document.AddConditionTo(or, COMPARE, new Vector2(-100f, -250f));
            _document.AddConditionTo(or, COMPARE, new Vector2(-100f, -150f));

            AgentCondition added = _document.AddConditionTo(routine.Reasoner, "aikit.condition.is", new Vector2(100f, -350f));

            var and = routine.Reasoner.Condition as AllCondition;
            Assert.IsNotNull(and, "Both must hold: the OR and the new question");
            CollectionAssert.AreEqual(new object[] { or, added }, and.Conditions);
            Assert.AreEqual(2, or.Conditions.Count, "The OR keeps its two alternatives");
        }

        [Test]
        public void CharacterSection_AnIsDroppedOnADecisionWithAnOr_IsAndedWithIt()
        {
            AgentState routine = Routine(out _);
            var or = (AnyCondition)_document.AddConditionTo(routine.Reasoner, "aikit.condition.any", new Vector2(100f, -200f));
            _document.AddConditionTo(or, COMPARE, new Vector2(-100f, -250f));
            _document.AddConditionTo(or, COMPARE, new Vector2(-100f, -150f));

            CharacterSection.Wire(_document, CharacterSection.Rows.Single(row => row.Name == "Is Blocking"), routine,
                new Vector2(100f, -350f));

            var and = routine.Reasoner.Condition as AllCondition;
            Assert.IsNotNull(and);
            Assert.AreSame(or, and.Conditions[0]);
            Assert.IsInstanceOf<IsCondition>(and.Conditions[1]);
            Assert.AreEqual(CharacterState.Blocking, ((IsCondition)and.Conditions[1]).State);
            Assert.AreEqual(Whose.Me, ((IsCondition)and.Conditions[1]).Of);
        }

        // ---------- a read-only graph takes nothing ----------

        [Test]
        public void GraphDocument_AddShapeOrSensor_OnAReadOnlyGraph_ChangesNothing()
        {
            OldModelAssets.Write(_asset, OldModelAssets.REFLEX);
            LogAssert.ignoreFailingMessages = true;         // the refusal is said once
            var document = new GraphDocument(_asset);
            Assert.IsTrue(document.IsReadOnly, "Set-up: a graph of the old model");
            int sensors = document.Sensors().Count;
            string before = JsonUtility.ToJson(_asset);

            Assert.IsNull(document.AddShape("aikit.sensor.radius", new Vector2(-300f, 0f)));
            Assert.IsNull(document.Detection(), "No Detection made, not even in memory");
            Assert.IsNull(document.AddSensor("aikit.sensor.time", new Vector2(-300f, 200f)));
            Assert.AreEqual(sensors, document.Sensors().Count);
            Assert.IsNull(document.AddVariableNodeAt(BehaviorGraphInstance.DELTA_TIME, new Vector2(-300f, 400f)));
            Assert.AreEqual(before, JsonUtility.ToJson(_asset), "The asset is as it was");
        }

        // ---------- every new node waits for a free place ----------

        [Test]
        public void NodeMenu_AConditionWithNoAsker_TheNewBranchWaitsForAPlaceToo()
        {
            _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            _document.Placed();
            NodeMenu.Item compare = NodeMenu.Conditions(_document).Single(item => item.Id == COMPARE);

            var condition = (AgentCondition)NodeMenu.Add(_document, compare, Vector2.zero);

            AgentState branch = _document.States().Single(state => state.Tasks.FirstOrDefault() is BranchTask);
            CollectionAssert.Contains(_document.Waiting, branch, "The Branch made for the condition waits for a place");
            CollectionAssert.Contains(_document.Waiting, condition, "…and so does the condition");
            Assert.AreEqual(Vector2.zero, _document.PositionOf(branch), "Made where the list was opened");
            Assert.AreEqual(new Vector2(-240f, 70f), _document.PositionOfNode(condition));
        }

        [Test]
        public void GraphDocument_TheFirstShape_ItsNewDetectionWaitsForAPlaceToo()
        {
            var at = new Vector2(-400f, 100f);

            SensorTask shape = _document.AddShape("aikit.sensor.radius", at);

            DetectionTask detection = _document.Detection();
            CollectionAssert.Contains(_document.Waiting, detection, "Made with the first shape: it waits for a place");
            CollectionAssert.Contains(_document.Waiting, shape);
            Assert.AreEqual(at + new Vector2(280f, 0f), _document.PositionOfNode(detection));
        }

        // ---------- frames ----------

        [Test]
        public void GraphDocument_Frames_AddRenameMembership()
        {
            AgentState a = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            AgentState b = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(0f, 200f));
            AgentState c = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(0f, 400f));
            GraphFrame first = _document.AddFrame("First", new[] { a.Id });

            GraphFrame melee = _document.AddFrame("Melee", new[] { a.Id, b.Id });

            CollectionAssert.IsEmpty(first.Members, "A node is in one frame: it left the first");
            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, melee.Members);
            Assert.IsNull(_document.AddFrame("Empty", new int[0]), "A frame round nothing is not made");

            _document.RenameFrame(melee, "Close");
            Assert.AreEqual("Close", melee.Title);

            _document.SetFrameMember(first, c.Id, true);
            CollectionAssert.AreEqual(new[] { c.Id }, first.Members);
            CollectionAssert.DoesNotContain(melee.Members, c.Id);
            _document.SetFrameMember(first, 0, true);
            CollectionAssert.AreEqual(new[] { c.Id }, first.Members, "No node: no member");

            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", new Vector2(-400f, 0f));
            AgentState routine = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(400f, 0f));
            _document.Connect(brain, routine);
            _document.EnsureBrainFrames();
            Assert.AreEqual(brain.Id, _document.BrainOf(brain.Id), "The Brain is its frame's");
            Assert.AreEqual(0, _document.BrainOf(routine.Id), "A decision stands outside the Brain's frame");
        }

        [Test]
        public void GraphDocument_EnsureBrainFrames_AFrameOfANonBrainBecomesPlain_ABrainGetsOne()
        {
            AgentState wait = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", new Vector2(-400f, 0f));
            _asset.Frames.Add(new GraphFrame { Title = "Old", Brain = wait.Id, Members = { wait.Id } });

            _document.EnsureBrainFrames();

            GraphFrame old = _asset.Frames.Single(frame => frame.Title == "Old");
            Assert.AreEqual(0, old.Brain, "A frame whose brain is not a Brain is a plain one");
            CollectionAssert.AreEqual(new[] { wait.Id }, old.Members, "…its nodes kept");
            GraphFrame own = _asset.Frames.Single(frame => frame.Brain == brain.Id);
            Assert.AreEqual("Brain", own.Title, "The Brain gets a frame of its own, by its type");
            CollectionAssert.IsEmpty(own.Members);
        }

        [Test]
        public void GraphDocument_EnsureBrainFrames_TheBrainsFrameHoldsTheBrainAlone()
        {
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState wait = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(400f, 0f));
            SensorTask sensor = _document.AddSensor("aikit.sensor.time", new Vector2(-400f, 0f));
            _asset.Frames.Add(new GraphFrame { Title = "Brain", Brain = brain.Id, Members = { sensor.Id, wait.Id } });

            _document.EnsureBrainFrames();

            CollectionAssert.IsEmpty(_asset.Frames.Single(frame => frame.Brain == brain.Id).Members,
                "A sensor or a block dragged in is no part of the Brain");
        }

        [Test]
        public void GraphDocument_EnsureBrainFrames_OnAReadOnlyGraph_TouchesNothing()
        {
            OldModelAssets.Write(_asset, OldModelAssets.REFLEX);
            LogAssert.ignoreFailingMessages = true;
            var document = new GraphDocument(_asset);
            string before = JsonUtility.ToJson(_asset);

            document.EnsureBrainFrames();

            Assert.AreEqual(before, JsonUtility.ToJson(_asset));
        }

        // ---------- bend points and links ----------

        private Transition Arrow(out AgentState left, out AgentState right)
        {
            left = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            right = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(400f, 0f));
            return _document.Connect(left, right, TransitionTrigger.TasksSucceeded);
        }

        [Test]
        public void GraphDocument_Waypoints_InsertClampedMoveRemove_KeptWithTheGraph()
        {
            Transition arrow = Arrow(out AgentState left, out _);
            var p0 = new Vector2(100f, 50f);
            var p1 = new Vector2(200f, 50f);

            _document.AddWaypoint(arrow, 99, p1);
            CollectionAssert.AreEqual(new[] { p1 }, _document.WaypointsOf(arrow), "An index past the end: at the end");
            _document.AddWaypoint(arrow, 0, p0);
            CollectionAssert.AreEqual(new[] { p0, p1 }, _document.WaypointsOf(arrow));
            _document.MoveWaypoint(arrow, 5, Vector2.one);
            CollectionAssert.AreEqual(new[] { p0, p1 }, _document.WaypointsOf(arrow), "No such point: nothing moves");
            _document.RemoveWaypoint(arrow, 0);
            CollectionAssert.AreEqual(new[] { p1 }, _document.WaypointsOf(arrow));

            AgentTask block = left.Tasks[0];
            var bend = new Vector2(-50f, 20f);
            _document.AddWaypoint(block, 0, bend);          // a data wire into the block, bent

            var reopened = new GraphDocument(_asset);
            AgentState readLeft = reopened.States().Single(state => state.Id == left.Id);
            CollectionAssert.AreEqual(new[] { p1 }, reopened.WaypointsOf(readLeft.Transitions.Single()), "Kept with the graph");
            CollectionAssert.AreEqual(new[] { bend }, reopened.WaypointsOf(readLeft.Tasks[0]));
        }

        [Test]
        public void GraphDocument_ClearLink_AndRetargetBack()
        {
            Transition arrow = Arrow(out AgentState left, out _);
            AgentState back = _document.AddStateWithTask(null, "aikit.task.wait", new Vector2(-200f, 0f));

            _document.SetLink(arrow);
            Assert.IsTrue(_document.IsLink(arrow));
            _document.ClearLink(arrow);

            Assert.IsFalse(_document.IsLink(arrow), "A wire all the way again");
            var reopened = new GraphDocument(_asset);
            Transition read = reopened.States().Single(state => state.Id == left.Id).Transitions.Single();
            Assert.IsFalse(reopened.IsLink(read), "…kept with the graph");
            _document.SetLink(arrow);
            reopened = new GraphDocument(_asset);
            Assert.IsTrue(reopened.IsLink(reopened.States().Single(state => state.Id == left.Id).Transitions.Single()),
                "Set-up of the check: a link is kept too");
            _document.ClearLink(arrow);

            _document.Retarget(arrow, back);
            Assert.IsTrue(_document.IsDocked(arrow), "Pointed back across the canvas: a link docked at its pin");
        }

        // ---------- keys, values, conditions ----------

        [Test]
        public void GraphDocument_CheckKeys_AMoveNoWeaponHas_IsAWarningOnItsBlock()
        {
            Assume.That(NodeOptions.Moves().Count > 0, "The project has a weapon with moves");
            AgentState attack = _document.AddStateWithTask(null, "aikit.task.attack", Vector2.zero);
            var task = (AttackTask)attack.Tasks[0];
            task.Move = "Zzz Not A Move";

            _document.Touch();

            GraphIssue issue = _document.Issues.Single(found => found.Code == GraphIssueCode.UnknownKey);
            Assert.IsFalse(issue.IsError, "A warning: a typo, or a move renamed");
            Assert.AreEqual(task.Id, issue.RecordId, "On its block");
            StringAssert.Contains("Move 'Zzz Not A Move' is not in the project", issue.Message);

            task.Move = "";
            _document.Touch();
            Assert.IsFalse(_document.Issues.Exists(found => found.Code == GraphIssueCode.UnknownKey), "No move: no key to check");
        }

        [Test]
        public void GraphDocument_WireValue_AnAssetLinkOrAWrongValueType_IsRefused()
        {
            AgentState use = _document.AddStateWithTask(null, "aikit.task.use-ability", Vector2.zero);
            AgentState reading = _document.AddStateWithTask(null, "aikit.tests.value-reader", new Vector2(400f, 0f));
            AgentValue what = _document.AddValue("aikit.value.what", new Vector2(-300f, 0f));
            string before = JsonUtility.ToJson(_asset);

            Assert.IsFalse(_document.WireValue(what, use.Tasks[0], nameof(UseAbilityTask.Ability)),
                "An ability is picked on the node: no wire comes into it");
            Assert.IsNull(_document.AddValueTo(reading.Tasks[0], nameof(TestValueReaderTask.Numbers), "aikit.value.what",
                Vector2.zero), "A pin takes only the values of its kind");
            Assert.AreEqual(before, JsonUtility.ToJson(_asset), "Nothing changed");
        }

        [Test]
        public void GraphDocument_UnwireValue_FromAListPin_RemovesOnlyThatOne()
        {
            AgentState reading = _document.AddStateWithTask(null, "aikit.tests.value-reader", Vector2.zero);
            var reader = (TestValueReaderTask)reading.Tasks[0];
            AgentValue v1 = _document.AddValueTo(reader, nameof(TestValueReaderTask.Numbers), "aikit.tests.number-value",
                new Vector2(-300f, 0f));
            AgentValue v2 = _document.AddValueTo(reader, nameof(TestValueReaderTask.Numbers), "aikit.tests.number-value",
                new Vector2(-300f, 100f));

            _document.UnwireValue(v1, reader, nameof(TestValueReaderTask.Numbers));

            CollectionAssert.AreEqual(new[] { v2 }, reader.Numbers, "Only that wire is pulled out");
            CollectionAssert.Contains(_document.Values(), v1, "The node stays on the canvas");
            Assert.AreEqual(0, _document.ReadersOf(v1).Count);
            Assert.AreEqual(2, new GraphDocument(_asset).Values().Count, "Both kept with the graph");
        }

        [Test]
        public void GraphDocument_AddConditionTo_AConditionThatAsksNothing_AddsNothing()
        {
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], COMPARE, new Vector2(-300f, 0f));
            _document.Placed();
            int records = _asset.Records.Count;
            string before = JsonUtility.ToJson(compare);

            Assert.IsNull(_document.AddConditionTo(compare, "aikit.condition.is", new Vector2(-500f, 0f)),
                "A Compare takes no condition");
            Assert.AreEqual(before, JsonUtility.ToJson(compare));
            Assert.AreEqual(records, _asset.Records.Count, "No record made");
            Assert.IsNull(_document.JustAdded, "Nothing waits for a place");
        }

        // ---------- sensors ----------

        [Test]
        public void GraphDocument_RemoveSensorAndShape()
        {
            _document.DeclareVariable("Seen", VariableType.Transform);
            SensorTask first = _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            _document.AddShape("aikit.sensor.cone", new Vector2(-400f, 200f));
            DetectionTask detection = _document.Detection();
            _document.SetSensorVariable(detection, "Seen");

            SensorTask time = _document.AddSensor("aikit.sensor.time", new Vector2(-400f, 400f));
            Assert.AreEqual("", time.StoreIn, "A new sensor writes nowhere until given a variable");
            Assert.AreSame(time, _document.JustAdded);

            _document.RemoveShape(detection, first);
            Assert.AreEqual(1, detection.Sensors.Count);
            _document.RemoveSensor(detection);
            Assert.IsNull(_document.Detection(), "Detection goes with its shapes");
            CollectionAssert.AreEqual(new[] { time }, _document.Sensors());
            Assert.IsTrue(_document.IsDeclared("Seen"), "Its variable belongs to the blackboard: it stays");
        }

        // ---------- opening ----------

        [Test]
        public void GraphDocument_ReopeningAGraphThatNeedsNothing_DoesNotTouchIt()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new DailyRoutineReasoner(), 1), new WaitTask(1f));
            BehaviorGraph graph = g.Build();
            NodeTitles.NameByType(graph);
            GraphSerializer.Write(graph, _asset);
            Assert.AreEqual(GraphUpgrades.Current, _asset.FormatVersion, "Set-up: the current format");
            string before = JsonUtility.ToJson(_asset);

            var document = new GraphDocument(_asset);

            Assert.IsFalse(document.IsReadOnly);
            Assert.AreEqual(before, JsonUtility.ToJson(_asset), "Opened, not saved");
        }
    }
}
