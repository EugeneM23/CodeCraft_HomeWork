using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The nodes a decision's chain picks between blocks with (step 69): the numbers and flags of the blackboard —
    /// Set, Add, Subtract, Multiply, Divide, Set Flag — the question Compare asks of them, the Branch that splits the
    /// flow by it (with AND / OR), and Debug Log that shows a value.
    /// </summary>
    public sealed class DataNodeTests
    {
        private const string COUNT = "Attacks";
        private const string OTHER = "Limit";
        private const string FLAG = "Angry";
        private const string OBJECT = "Target";

        private GraphRig _rig;
        private Blackboard Board => _rig.Context.Blackboard;

        [SetUp]
        public void SetUp() => _rig = new GraphRig(trace: false);

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        // ---------- the numbers ----------

        [Test]
        public void Math_EachNodeChangesTheVariableByValue_AndIsDoneAtOnce()
        {
            Board.Set(COUNT, 6f);

            Assert.AreEqual(TaskStatus.Success, Tick(new AddNumberTask(COUNT, 2f)));
            Assert.AreEqual(8f, Board.Get(COUNT, 0f));
            Tick(new SubtractNumberTask { Variable = COUNT, Value = 3f });
            Assert.AreEqual(5f, Board.Get(COUNT, 0f));
            Tick(new MultiplyNumberTask { Variable = COUNT, Value = 4f });
            Assert.AreEqual(20f, Board.Get(COUNT, 0f));
            Tick(new DivideNumberTask { Variable = COUNT, Value = 8f });
            Assert.AreEqual(2.5f, Board.Get(COUNT, 0f));
            Tick(new SetNumberTask { Variable = COUNT, Value = -1f });
            Assert.AreEqual(-1f, Board.Get(COUNT, 0f));
        }

        [Test]
        public void Math_AVariableNotThereYet_CountsFromZero()
        {
            Tick(new AddNumberTask(COUNT, 1f));

            Assert.AreEqual(1f, Board.Get(COUNT, 0f));
        }

        [Test]
        public void Math_DivideByZero_LeavesTheNumberAsItIs()
        {
            Board.Set(COUNT, 7f);

            Tick(new DivideNumberTask { Variable = COUNT, Value = 0f });

            Assert.AreEqual(7f, Board.Get(COUNT, 0f));
        }

        [Test]
        public void Math_ValueWiredFromAnotherVariable_UsesThatVariable()
        {
            Board.Set(COUNT, 10f);
            Board.Set(OTHER, 0.25f);

            Tick(new SubtractNumberTask { Variable = COUNT, Value = BlackboardValue<float>.Variable(OTHER) });

            Assert.AreEqual(9.75f, Board.Get(COUNT, 0f), 1e-5f);
        }

        [Test]
        public void Math_NoVariableNamed_ChangesNothing_AndStillIsDone()
        {
            Assert.AreEqual(TaskStatus.Success, Tick(new AddNumberTask()));
            Assert.AreEqual(TaskStatus.Success, Tick(new SetFlagTask()));
        }

        [Test]
        public void Math_AndSetFlag_AreInstant_SoAnOnTickChainRunsThemInOneThink()
        {
            Assert.IsTrue(new SetNumberTask().IsInstant);
            Assert.IsTrue(new AddNumberTask().IsInstant);
            Assert.IsTrue(new SetFlagTask().IsInstant);
            Assert.IsTrue(new BranchTask().IsInstant);
            Assert.IsTrue(new DebugLogTask().IsInstant);
        }

        [Test]
        public void SetFlag_WritesOnOrOff()
        {
            Tick(new SetFlagTask { Variable = FLAG, Value = true });
            Assert.IsTrue(Board.Get(FLAG, false));

            Tick(new SetFlagTask { Variable = FLAG, Value = false });
            Assert.IsFalse(Board.Get(FLAG, true));
        }

        // ---------- Compare ----------

        [TestCase(NumberComparison.Less, 2f, 3f, true)]
        [TestCase(NumberComparison.Less, 3f, 3f, false)]
        [TestCase(NumberComparison.LessOrEqual, 3f, 3f, true)]
        [TestCase(NumberComparison.Greater, 3f, 3f, false)]
        [TestCase(NumberComparison.Greater, 4f, 3f, true)]
        [TestCase(NumberComparison.GreaterOrEqual, 3f, 3f, true)]
        [TestCase(NumberComparison.GreaterOrEqual, 2.9f, 3f, false)]
        public void Compare_ANumber_ByEachWay(NumberComparison how, float number, float value, bool expected)
        {
            Board.Set(COUNT, number);

            Assert.AreEqual(expected, new CompareCondition(COUNT, how, value).Check(_rig.Context));
        }

        [Test]
        public void Compare_WithSlack_TwoThresholds_TakesAtValue_LetsGoOnlyBeyondValuePlusSlack()
        {
            var near = new CompareCondition(COUNT, NumberComparison.Less, 2f, slack: 0.5f);

            Board.Set(COUNT, 2.2f);
            Assert.IsFalse(near.Check(_rig.Context), "Not taken until below 2");
            Board.Set(COUNT, 1.9f);
            Assert.IsTrue(near.Check(_rig.Context));
            Board.Set(COUNT, 2.4f);
            Assert.IsTrue(near.Check(_rig.Context), "Holding: kept until 2 + 0.5");
            Board.Set(COUNT, 2.6f);
            Assert.IsFalse(near.Check(_rig.Context), "Let go beyond the slack");
            Board.Set(COUNT, 2.4f);
            Assert.IsFalse(near.Check(_rig.Context), "Not held any more: the near threshold again");
        }

        [Test]
        public void Compare_WithSlack_GreaterWays_LetGoBelowValueMinusSlack()
        {
            var many = new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f, slack: 1f);

            Board.Set(COUNT, 3f);
            Assert.IsTrue(many.Check(_rig.Context));
            Board.Set(COUNT, 2.2f);
            Assert.IsTrue(many.Check(_rig.Context), "Held until below 3 - 1");
            Board.Set(COUNT, 1.9f);
            Assert.IsFalse(many.Check(_rig.Context));
        }

        [Test]
        public void Compare_WithAnotherVariableWiredIntoB_ComparesTheTwo()
        {
            Board.Set(COUNT, 5f);
            Board.Set(OTHER, 6f);
            var compare = new CompareCondition(COUNT, NumberComparison.Less, 100f) { Other = OTHER };

            Assert.IsTrue(compare.Check(_rig.Context));
            Board.Set(OTHER, 4f);
            Assert.IsFalse(compare.Check(_rig.Context), "B is the variable, not the number of the node");
        }

        [Test]
        public void Compare_AFlag_IsItOnOrOff()
        {
            var on = new CompareCondition(FLAG, VariableType.Bool);
            var off = new CompareCondition(FLAG, VariableType.Bool, expected: false);

            Assert.IsFalse(on.Check(_rig.Context), "A flag not written yet is off");
            Assert.IsTrue(off.Check(_rig.Context));
            Board.Set(FLAG, true);
            Assert.IsTrue(on.Check(_rig.Context));
            Assert.IsFalse(off.Check(_rig.Context));
        }

        [Test]
        public void Compare_AnObject_IsThereOneOrNot()
        {
            var there = new CompareCondition(OBJECT, VariableType.Transform);
            var none = new CompareCondition(OBJECT, VariableType.Transform, expected: false);

            Assert.IsFalse(there.Check(_rig.Context));
            Assert.IsTrue(none.Check(_rig.Context));
            Board.Set(OBJECT, _rig.CreateTargetAt(Vector3.one));
            Assert.IsTrue(there.Check(_rig.Context));
            Assert.IsFalse(none.Check(_rig.Context));
        }

        [Test]
        public void Compare_AnIntVariable_IsReadAsANumber()
        {
            Board.Set(COUNT, 3);

            Assert.IsTrue(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f) { Kind = VariableType.Int }
                .Check(_rig.Context));
        }

        [Test]
        public void Compare_NoVariable_IsFalse()
        {
            Assert.IsFalse(new CompareCondition().Check(_rig.Context));
            Assert.IsFalse(new CompareCondition(COUNT, NumberComparison.Less, 1f).Check(null));
        }

        // ---------- Branch ----------

        [Test]
        public void Branch_TrueWhenItsConditionHolds_FalseWhenNot()
        {
            var branch = new BranchTask(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f));

            Board.Set(COUNT, 3f);
            Assert.AreEqual(TaskStatus.Success, Tick(branch), "True");
            Board.Set(COUNT, 2f);
            Assert.AreEqual(TaskStatus.Failure, Tick(branch), "False");
        }

        [Test]
        public void Branch_AndOr_AskEveryInput()
        {
            var many = new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f);
            var angry = new CompareCondition(FLAG, VariableType.Bool);
            var both = new BranchTask(new AllCondition(many, angry));
            var either = new BranchTask(new AnyCondition(many, angry));

            Board.Set(COUNT, 5f);
            Assert.AreEqual(TaskStatus.Failure, Tick(both), "AND: one is not enough");
            Assert.AreEqual(TaskStatus.Success, Tick(either), "OR: one is enough");
            Board.Set(FLAG, true);
            Assert.AreEqual(TaskStatus.Success, Tick(both));
            Board.Set(COUNT, 0f);
            Board.Set(FLAG, false);
            Assert.AreEqual(TaskStatus.Failure, Tick(either), "OR: none");
        }

        [Test]
        public void Branch_NoConditionWiredIn_IsFalse_WhateverTheBlackboardHolds()
        {
            Board.Set(FLAG, true);

            Assert.AreEqual(TaskStatus.Failure, Tick(new BranchTask()), "Nothing wired in: False — a broken wire gives no answer");
        }

        // ---------- Debug Log ----------

        [Test]
        public void DebugLog_WritesTheMessage_WithTheAgentsName()
        {
            LogAssert.Expect(LogType.Log, $"[{_rig.Agent.name}] Got here");

            Assert.AreEqual(TaskStatus.Success, Tick(new DebugLogTask { Message = "Got here" }));
        }

        [Test]
        public void DebugLog_AVariableOfAnyKind_IsWrittenAfterTheMessage()
        {
            Board.Set(COUNT, 2.4f);
            Board.Set(FLAG, true);
            Board.Set(OBJECT, _rig.CreateTargetAt(Vector3.zero));
            string name = _rig.Agent.name;

            LogAssert.Expect(LogType.Log, $"[{name}] Timer {COUNT} = 2.4");
            Tick(new DebugLogTask { Message = "Timer", Value = COUNT });
            LogAssert.Expect(LogType.Log, $"[{name}] Mood {FLAG} = yes");
            Tick(new DebugLogTask { Message = "Mood", Value = FLAG });
            LogAssert.Expect(LogType.Log, $"[{name}] Who {OBJECT} = Target");
            Tick(new DebugLogTask { Message = "Who", Value = OBJECT });
            LogAssert.Expect(LogType.Log, $"[{name}] What Nobody = (none)");
            Tick(new DebugLogTask { Message = "What", Value = "Nobody" });
        }

        [Test]
        public void DebugLog_AnObjectThatIsGone_IsNothing()
        {
            Board.Set(OBJECT, _rig.CreateTargetAt(Vector3.zero));
            _rig.DestroyTarget();

            LogAssert.Expect(LogType.Log, new Regex(@"Who Target = \(nothing\)$"));
            Tick(new DebugLogTask { Message = "Who", Value = OBJECT });
        }

        private TaskStatus Tick(AgentTask task) => task.Tick(_rig.Context, 0.1f);
    }
}
