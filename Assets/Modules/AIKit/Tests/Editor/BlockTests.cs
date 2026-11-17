using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Block block (step 90, reactions; handbook §19.2): the guard is held up for Hold seconds — the character is
    /// blocking, shows the action Block, stands and turns to the target — then lowered (its ending shown); taken away, it
    /// drops at once. The flinch of a hit is the body's (AgentContext.HurtAt, played by the animator), not a block.
    /// </summary>
    public sealed class BlockTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _rig.Context.Actions.View = new ShowingView(_rig.Context.Actions);
            _target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _target.transform.position = new Vector3(0f, 0f, 3f);          // to the left of the body, which faces +X
            _rig.Context.Blackboard.Set(TARGET, _target.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            GraphRig.DestroyAll();
        }

        private TaskStatus Step(AgentTask task)
        {
            _rig.Context.Advance(DT);
            TaskStatus status = task.Tick(_rig.Context, DT);
            _rig.Actuator.Apply(DT);
            return status;
        }

        [Test]
        public void Block_HoldsTheGuardForHold_BlockingAndShowingBlock_ThenLowersIt()
        {
            var block = new BlockTask { Target = TARGET, Hold = 0.5f };
            float hold = block.Hold.Get(null);

            Assert.AreEqual(TaskStatus.Running, Step(block));
            Assert.IsTrue(_rig.Context.IsBlocking, "The guard is up: the character is blocking");
            Assert.AreEqual(ActionState.Kind.Action, _rig.Context.Actions.Current);
            Assert.AreEqual(block.Action, _rig.Context.Actions.Name, "It shows the action Block");
            Assert.Greater(_rig.Actuator.Stops, 0, "The body stands");

            float held = DT;
            TaskStatus status = TaskStatus.Running;
            while (status == TaskStatus.Running && held < 2f)
            {
                status = Step(block);
                held += DT;
            }

            Assert.AreEqual(TaskStatus.Success, status, "Done — the guard is lowered");
            Assert.AreEqual(hold, held, DT + 1e-4f, "Held for Hold seconds");
            Assert.IsFalse(_rig.Context.IsBlocking);
            Assert.AreEqual(ActionState.Kind.None, _rig.Context.Actions.Current);
            Assert.IsTrue(_rig.Context.Actions.EndsGently, "Held to its end: the ending of the action is shown");
        }

        [Test]
        public void Block_TurnsTheGuardToTheTarget()
        {
            var block = new BlockTask { Target = TARGET, Hold = 1f };

            Step(block);

            Vector3 to = _target.transform.position - _rig.Actuator.Position;
            Assert.Less(Vector3.Angle(_rig.Actuator.Facing, to), 1f, "Facing the one it blocks");
        }

        [Test]
        public void Block_NoTargetOfItsOwn_TurnsToTheBrainsTarget()
        {
            var block = new BlockTask { Hold = 1f };
            _rig.Context.BrainTarget = _target.transform;

            Step(block);

            Vector3 to = _target.transform.position - _rig.Actuator.Position;
            Assert.Less(Vector3.Angle(_rig.Actuator.Facing, to), 1f);
        }

        [Test]
        public void Block_TakenAway_TheGuardDropsAtOnce()
        {
            var block = new BlockTask { Target = TARGET, Hold = 1f };
            Step(block);

            block.Interrupt(_rig.Context);

            Assert.IsFalse(_rig.Context.IsBlocking, "Not blocking once taken away");
            Assert.AreEqual(ActionState.Kind.None, _rig.Context.Actions.Current);
            Assert.IsFalse(_rig.Context.Actions.EndsGently, "Dropped at once: the character has to go");
        }

        [Test]
        public void Block_AnActionAskedForAfterTheGuard_IsNotEndedByIt()
        {
            var block = new BlockTask { Target = TARGET, Hold = 1f };
            Step(block);
            _rig.Context.Actions.Begin("Taunt");

            block.Interrupt(_rig.Context);

            Assert.AreEqual("Taunt", _rig.Context.Actions.Name, "Somebody else's action stays");
            Assert.IsFalse(_rig.Context.IsBlocking);
        }

        [Test]
        public void Block_InAGraph_IsDoneAfterHold_AndTheChainGoesOn()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            AgentState[] chain = g.Chain(g.Always(), new BlockTask { Target = TARGET, Hold = 0.4f }, new WaitTask(100f));
            _rig.ThinkInterval = 0f;
            _rig.SetGraph(g.Build());
            _rig.Agent.Blackboard.Set(TARGET, _target.transform);

            Assert.IsTrue(_rig.StepUntil(() => _rig.Context.IsBlocking, 1f, DT), "The guard goes up");
            Assert.IsTrue(_rig.StepUntil(() => _rig.Instance.Current == chain[1], 1f, DT), "…and down: Done leads on");
            Assert.IsFalse(_rig.Context.IsBlocking);
        }
    }
}
