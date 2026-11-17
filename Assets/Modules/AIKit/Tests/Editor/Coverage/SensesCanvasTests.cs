using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// What a condition reads without a wire is said on its node (step-90 recheck; the project's rule: no implicit
    /// variables — an explicit mark on the node): Attack Slot asks about the Brain's target, and its node says so.
    /// </summary>
    public sealed class SensesCanvasTests
    {
        private const string BRANCH = "aikit.task.branch";
        private const string ATTACK_SLOT = "aikit.condition.attack-slot";
        private const string COMPARE = "aikit.condition.compare";

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

        [Test]
        public void Canvas_AnAttackSlotCondition_SaysTargetBrainsOnItsNode()
        {
            AgentState slotBranch = _document.AddStateWithTask(null, BRANCH, Vector2.zero);
            AgentState compareBranch = _document.AddStateWithTask(null, BRANCH, new Vector2(0f, 400f));
            Assume.That(_document.AddConditionTo(slotBranch.Tasks[0], ATTACK_SLOT, new Vector2(-300f, 0f)),
                Is.InstanceOf<AttackSlotCondition>());
            Assume.That(_document.AddConditionTo(compareBranch.Tasks[0], COMPARE, new Vector2(-300f, 400f)),
                Is.InstanceOf<CompareCondition>());

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Node slot = NodeTitled(canvas, "Attack Slot");
            Assert.IsNotNull(slot, "The condition is drawn");
            Label mark = slot.Q<Label>(GraphViewCanvas.REQUIREMENT);
            Assert.IsNotNull(mark, "The node says whose target it asks about");
            Assert.AreEqual(new AttackSlotCondition().Requirement, mark.text);
            Assert.AreEqual("target: Brain's", mark.text);

            Node compare = NodeTitled(canvas, "Compare");
            Assert.IsNotNull(compare);
            Assert.IsNull(compare.Q<Label>(GraphViewCanvas.REQUIREMENT), "A Compare reads only what is wired into it");
        }

        private static Node NodeTitled(GraphViewCanvas canvas, string title) =>
            canvas.Query<Node>().ToList().FirstOrDefault(node => node.title == title);
    }
}
