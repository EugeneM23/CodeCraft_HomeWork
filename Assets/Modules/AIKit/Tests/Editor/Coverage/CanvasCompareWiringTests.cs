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
    /// What comes into a Compare (and the other readers of a number) and how it is drawn: a value node wired into A takes
    /// the place of the variable it had — the Compare is a number comparison then, drawn as one, with one A pin, and what
    /// runs is what is drawn; a value that is replaced stays on the canvas unread (as a wire's does), the new one waits
    /// for a free place; a state of the Character section is joined to a question by an AND, whatever the question is.
    /// </summary>
    public sealed class CanvasCompareWiringTests
    {
        private const string COMPARE = "aikit.condition.compare";
        private const string BRANCH = "aikit.task.branch";

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
            GraphRig.DestroyAll();
        }

        private static CharacterRow Row(string name) => CharacterSection.Rows.Single(row => row.Name == name);

        private static List<Node> Nodes(GraphViewCanvas canvas) => canvas.Query<Node>().ToList();

        private static int PinsNamed(Node node, string name) =>
            node.inputContainer.Query<Port>().ToList().Count(port => port.portName == name);

        private CompareCondition NewCompare(out AgentState branch)
        {
            branch = _document.AddStateWithTask(null, BRANCH, Vector2.zero);
            return (CompareCondition)_document.AddConditionTo(branch.Tasks[0], COMPARE, new Vector2(-300f, 0f));
        }

        // ---------- a value into A takes the variable's place ----------

        [Test]
        public void CharacterSection_ANumberDroppedOnAFlag_MakesItANumberCompare_ShownAsCompare()
        {
            _document.DeclareVariable("Alarm", VariableType.Bool);
            AgentState branch = _document.AddStateWithTask(null, BRANCH, Vector2.zero);
            NodeMenu.Item alarm = NodeMenu.Conditions(_document).Single(item => item.Id == NodeMenu.FLAG + "Alarm");
            var flag = (CompareCondition)NodeMenu.Add(_document, alarm, new Vector2(-300f, 0f), branch.Tasks[0]);
            Assert.AreEqual(VariableType.Bool, flag.Kind, "Set-up: the Branch asks the flag");

            AgentNode get = CharacterSection.Wire(_document, Row("Health %"), flag, new Vector2(-500f, 0f));

            Assert.AreSame(get, flag.A);
            Assert.AreEqual(VariableType.Float, flag.Kind, "A number is compared now");
            Assert.AreEqual("", flag.Variable, "The flag is asked no more: A is the Get");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.IsFalse(Nodes(canvas).Any(node => node.title == "Alarm"), "Nothing is drawn as the flag 'Alarm'");
            Node compare = Nodes(canvas).Single(node => node.title == "Compare");
            Assert.AreEqual(1, PinsNamed(compare, "A"), "One A: the Get wired into it");

            // What runs is what is drawn: Health % against the number of the Compare
            var rig = new GraphRig(trace: false);
            var health = rig.Agent.gameObject.AddComponent<FakeHealth>();
            health.CurrentHealth = 50f;
            health.MaxHealth = 100f;
            rig.Context.Blackboard.Set("Alarm", false);
            Assert.AreEqual(50f >= flag.Value, flag.Check(rig.Context));
        }

        [Test]
        public void GraphDocument_WireValueIntoACompareA_DropsItsVariableInput()
        {
            _document.DeclareVariable("Rage", VariableType.Float);
            CompareCondition compare = NewCompare(out _);
            GraphVariableNode rage = _document.AddVariableNodeAt("Rage", new Vector2(-500f, 0f));
            _document.SetReader(rage, compare);
            compare.Kind = VariableType.Float;
            _document.BindField(compare, typeof(CompareCondition).GetField(nameof(CompareCondition.Variable)), "Rage");
            AgentValue get = _document.AddValue("aikit.value.get", new Vector2(-500f, 100f));

            Assert.IsTrue(_document.WireValue(get, compare, nameof(CompareCondition.A)));

            Assert.AreSame(get, compare.A);
            Assert.AreEqual("", compare.Variable, "A is the Get: the variable is not read any more");
            Assert.AreEqual(VariableType.Float, compare.Kind);
            CollectionAssert.DoesNotContain(rage.Readers, compare.Id, "The variable's wire is pulled out");
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.AreEqual(1, PinsNamed(Nodes(canvas).Single(node => node.title == "Compare"), "A"));
        }

        // ---------- a value replaced stays, the new one waits for a place ----------

        [Test]
        public void CharacterSection_AThirdNumberOnACompare_TheReplacedGetStaysUnread_NewOneWaitsForAPlace()
        {
            CompareCondition compare = NewCompare(out _);
            CharacterSection.Wire(_document, Row("Health %"), compare, new Vector2(-500f, 0f));
            CharacterSection.Wire(_document, Row("Max Health"), compare, new Vector2(-500f, 100f));
            AgentValue oldB = compare.B;

            CharacterSection.Wire(_document, Row("Run Speed"), compare, new Vector2(-500f, 200f));

            Assert.AreNotSame(oldB, compare.B);
            Assert.AreEqual(GetParameter.RunSpeed, ((GetValue)compare.B).Parameter);
            CollectionAssert.Contains(_document.Values(), oldB, "What came into B before stays on the canvas");
            Assert.AreEqual(0, _document.ReadersOf(oldB).Count, "…read by nobody");
            Assert.AreSame(compare.B, _document.JustAdded, "The new node waits for a free place");
            Assert.IsTrue(new GraphDocument(_asset).Values().OfType<GetValue>()
                .Any(value => value.Parameter == GetParameter.MaxHealth), "Kept with the graph");
        }
    }
}
