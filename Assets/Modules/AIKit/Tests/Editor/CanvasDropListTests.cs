using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// A wire let go over an empty place opens the list from any pin (the user's word, 15.11.2026): what reads a value
    /// (a value node's out, Try Get Component's Component and Found), what reads a variable (a variable node's out), what
    /// leads into a node (its In — the new node is wired by its outcome); and a paste goes where the pointer is.
    /// </summary>
    public sealed class CanvasDropListTests
    {
        private const string GET = "aikit.value.get";
        private const string IS = "aikit.condition.is";
        private const string COMPARE = "aikit.condition.compare";
        private const string SET = "aikit.task.set-number";
        private const string DEBUG_LOG = "aikit.task.debug-log";
        private const string CHASE = "aikit.task.chase";
        private const string TRY_GET = "aikit.task.try-get-component";
        private const string BRANCH = "aikit.task.branch";

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        [Test]
        public void Readers_OfAComponent_AGetAndAnIs_ThroughAs()
        {
            var look = new TryGetComponentTask("Target", typeof(Rigidbody));
            var fields = new Dictionary<string, string>();

            List<NodeMenu.Item> items = GraphViewCanvas.ReadersOf(_document, look.Component, fields);

            CollectionAssert.IsSubsetOf(new[] { GET, IS }, items.Select(item => item.Id).ToList());
            Assert.AreEqual(nameof(GetValue.As), fields[GET]);
            Assert.AreEqual(nameof(IsCondition.As), fields[IS]);
            Assert.IsFalse(items.Any(item => item.Id == SET), "A component is no number");
        }

        [Test]
        public void Readers_OfFound_ACompareInA_AndADebugLog_NotASet()
        {
            var look = new TryGetComponentTask("Target", typeof(Rigidbody));
            var fields = new Dictionary<string, string>();

            List<string> ids = GraphViewCanvas.ReadersOf(_document, look.Found, fields).Select(item => item.Id).ToList();

            CollectionAssert.Contains(ids, COMPARE);
            Assert.AreEqual(nameof(CompareCondition.A), fields[COMPARE]);
            CollectionAssert.Contains(ids, DEBUG_LOG);
            CollectionAssert.DoesNotContain(ids, SET, "A flag is no number to set");
            CollectionAssert.DoesNotContain(ids, GET);
            Assert.AreEqual(BRANCH, ids[0], "A flag leads a Branch first");
            Assert.AreEqual(nameof(BranchTask.Condition), fields[BRANCH]);
        }

        [Test]
        public void Readers_OfANumber_ASetAndACompare()
        {
            var fields = new Dictionary<string, string>();

            List<string> ids = GraphViewCanvas.ReadersOf(_document, new GetValue(GetParameter.Health), fields)
                .Select(item => item.Id).ToList();

            CollectionAssert.IsSubsetOf(new[] { SET, COMPARE, DEBUG_LOG }, ids);
        }

        [Test]
        public void Readers_OfAnObjectVariable_TheBlocksWithAnObjectPin_ACompare_ADebugLog()
        {
            var fields = new Dictionary<string, string>();

            List<string> ids = GraphViewCanvas.VariableReadersOf(_document, VariableType.Transform, fields)
                .Select(item => item.Id).ToList();

            CollectionAssert.IsSubsetOf(new[] { CHASE, TRY_GET, COMPARE, DEBUG_LOG }, ids);
            Assert.AreEqual(nameof(TryGetComponentTask.Object), fields[TRY_GET]);
            Assert.AreEqual(nameof(CompareCondition.Variable), fields[COMPARE]);
        }

        [Test]
        public void Readers_OfANumberVariable_NoObjectPin()
        {
            var fields = new Dictionary<string, string>();

            List<string> ids = GraphViewCanvas.VariableReadersOf(_document, VariableType.Float, fields)
                .Select(item => item.Id).ToList();

            CollectionAssert.Contains(ids, COMPARE);
            CollectionAssert.DoesNotContain(ids, TRY_GET, "Its Object pin takes an object");
            CollectionAssert.DoesNotContain(ids, BRANCH, "A number leads no Branch by itself");
        }

        [Test]
        public void Readers_OfAFlagVariable_ABranchFirst()
        {
            var fields = new Dictionary<string, string>();

            List<string> ids = GraphViewCanvas.VariableReadersOf(_document, VariableType.Bool, fields)
                .Select(item => item.Id).ToList();

            Assert.AreEqual(BRANCH, ids[0]);
            CollectionAssert.Contains(ids, COMPARE);
        }

        [Test]
        public void LeadingInto_ANewNodeIsWiredByItsOutcome()
        {
            AgentState look = _document.AddStateWithTask(null, TRY_GET, Vector2.zero);
            AgentState chase = _document.AddStateWithTask(null, CHASE, Vector2.zero);

            Assert.AreEqual(TransitionTrigger.TasksSucceeded, GraphViewCanvas.OutcomeInto(look), "Try Get Component: Then");
            Assert.IsNotNull(GraphViewCanvas.OutcomeInto(chase));
        }

        [Test]
        public void Paste_TheFirstWhereThePointerIs_TheOthersKeepTheirShape_NoPointer_NextToTheOriginals()
        {
            var copied = new List<Vector2> { new(100f, 100f), new(160f, 140f) };

            List<Vector2> places = GraphViewCanvas.PastePlaces(copied, new Vector2(500f, -20f));
            CollectionAssert.AreEqual(new[] { new Vector2(500f, -20f), new Vector2(560f, 20f) }, places);

            List<Vector2> beside = GraphViewCanvas.PastePlaces(copied, null);
            CollectionAssert.AreEqual(new[] { new Vector2(140f, 150f), new Vector2(200f, 190f) }, beside);
        }

        [Test]
        public void Duplicate_AtAPlace_GoesThere()
        {
            _document.DeclareVariable("Target", VariableType.Transform);
            GraphVariableNode node = _document.AddVariableNodeAt("Target", new Vector2(10f, 10f));

            GraphVariableNode copy = _document.DuplicateVariableNode(node, new Vector2(300f, 200f));

            Assert.AreEqual(new Vector2(300f, 200f), copy.Position);
        }
    }
}
