using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The numbers of the blackboard (step-90 recheck): Set / Add / Subtract / Multiply / Divide change an Int variable
    /// as a number — the canvas lets an Int variable into them — instead of reading 0 and throwing on the write; the
    /// blackboard's contract they rest on (one type per slot); and what a declared variable starts with.
    /// </summary>
    public sealed class SensesNumberTests
    {
        private const string COUNT = "Count";
        private const float DT = 0.02f;

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        [Test]
        public void NumberTask_AnIntVariable_IsChangedAsANumber_NoException()
        {
            var rig = new GraphRig(trace: false);
            Blackboard board = rig.Context.Blackboard;
            board.Set(COUNT, 3);                                    // an Int slot

            Assert.AreEqual(TaskStatus.Success, new AddNumberTask(COUNT, 2f).Tick(rig.Context, DT));
            Assert.AreEqual(5, board.Get(COUNT, 0), "3 + 2, read and written as the Int it is");
            new SubtractNumberTask { Variable = COUNT, Value = 1f }.Tick(rig.Context, DT);
            Assert.AreEqual(4, board.Get(COUNT, 0));
            new MultiplyNumberTask { Variable = COUNT, Value = 3f }.Tick(rig.Context, DT);
            Assert.AreEqual(12, board.Get(COUNT, 0));
            new DivideNumberTask { Variable = COUNT, Value = 5f }.Tick(rig.Context, DT);
            Assert.AreEqual(Mathf.RoundToInt(12f / 5f), board.Get(COUNT, 0), "A whole number stays whole: rounded");
            new SetNumberTask { Variable = COUNT, Value = 7f }.Tick(rig.Context, DT);
            Assert.AreEqual(7, board.Get(COUNT, 0));
            Assert.IsFalse(board.TryGetKey(COUNT, out BlackboardKey<float> _), "No float slot was made beside it");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NumberTask_AnIntVariableInAGraph_TheChainRunsOn_TheAgentKeepsStepping()
        {
            var g = new BrainGraphBuilder();
            g.Variable(COUNT, VariableType.Int);
            AgentState[] chain = g.Chain(g.Always(), new AddNumberTask(COUNT, 1f), new WaitTask(1f));
            chain[1].Name = "Waiting";
            var rig = new GraphRig(trace: false);
            rig.ThinkInterval = 0f;
            rig.SetGraph(g.Build());

            rig.StepSeconds(0.5f, DT);

            Assert.AreEqual(1, rig.Context.Blackboard.Get(COUNT, 0), "Added once, on the chain's first run");
            Assert.AreEqual("Waiting", rig.StateName, "Past the Add: the flow went on, the character was not frozen");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Blackboard_ByNameOfAnotherType_ANumberReadsAndWritesAsAnyNumber_AFlagStaysAnError()
        {
            var board = new Blackboard();
            board.Set("N", 3);

            Assert.AreEqual(3f, board.Get("N", 0f), "An int read as a float (15.11.2026: numbers of any type alike)");
            board.Set("N", 1.4f);
            Assert.AreEqual(1, board.Get("N", 0), "A float written into an int: kept an int, rounded");
            Assert.IsTrue(board.TryGetKey("N", out BlackboardKey<int> _), "Still one variable, of its own type");

            board.Set("Flag", true);
            Assert.AreEqual(0f, board.Get("Flag", 0f), "A flag is no number: the fallback");
            Assert.Throws<System.InvalidOperationException>(() => board.Set("Flag", 1f),
                "A number written into a flag: an error, not a second variable");
        }

        [Test]
        public void VariableDeclaration_DeclareOn_EveryTypeItsStart_APresentValueKept_ARemovedOneBack()
        {
            var board = new Blackboard();
            new VariableDeclaration("B", VariableType.Bool) { Bool = true }.DeclareOn(board);
            new VariableDeclaration("I", VariableType.Int) { Int = 3 }.DeclareOn(board);
            new VariableDeclaration("S", VariableType.String) { String = "a" }.DeclareOn(board);
            new VariableDeclaration("V", VariableType.Vector3) { Vector3 = new Vector3(1f, 2f, 3f) }.DeclareOn(board);
            new VariableDeclaration("T", VariableType.Transform).DeclareOn(board);

            Assert.IsTrue(board.Get("B", false));
            Assert.AreEqual(3, board.Get("I", 0));
            Assert.AreEqual("a", board.Get<string>("S"));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), board.Get("V", Vector3.zero));
            Assert.IsTrue(board.Has("T"), "An object variable is there…");
            Assert.IsNull(board.Get<Transform>("T"), "…holding nothing until bound");

            var f = new VariableDeclaration("F", VariableType.Float) { Float = 1.5f };
            board.Set("F", 9f);
            f.DeclareOn(board);
            Assert.AreEqual(9f, board.Get("F", 0f), "A value already there is kept");

            board.Remove("F");
            f.DeclareOn(board);
            Assert.AreEqual(1.5f, board.Get("F", 0f), "A removed one gets its start back");
        }

        [Test]
        public void NumberTask_AVariableOfAnotherKind_IsLeftAsItIs_NoException()
        {
            var rig = new GraphRig(trace: false);
            rig.Context.Blackboard.Set("Angry", true);

            Assert.AreEqual(TaskStatus.Success, new AddNumberTask("Angry", 1f).Tick(rig.Context, DT), "Done all the same");
            Assert.IsTrue(rig.Context.Blackboard.Get("Angry", false), "A flag is no number: untouched");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
