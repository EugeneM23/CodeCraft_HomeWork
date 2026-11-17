using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 3a: variables seen from the graph — reading is pure (a question about a variable nobody wrote makes no
    /// variable, on the agent's blackboard or the world's), and a value read from the world's scope follows the world
    /// the agent is in now.
    /// </summary>
    public class VariableGraphTests
    {
        private const float THINK = 0.1f;

        /// <summary>A block that holds the agent for as long as its decision does.</summary>
        private sealed class Hold : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        [Test]
        public void Condition_DoesNotWriteBlackboard()
        {
            var world = new AIWorld();
            var rig = new GraphRig(world: world) { ThinkInterval = THINK };

            // The routine asks again and again about variables nobody has written — a number against another number, a
            // flag, an object — and waits for a time read from the world's scope
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState ask = g.Block(new BranchTask(new AnyCondition(
                new CompareCondition("Ghost", NumberComparison.Greater, 0f) { Other = "Ghost Threshold" },
                new CompareCondition("Missing", VariableType.Bool),
                new CompareCondition("Nobody", VariableType.Transform))), "Ask");
            g.Do(routine, ask);
            g.Then(ask, g.Block(new Hold(), "Never"));
            g.OnFail(ask, g.Block(new WaitTask
            {
                Seconds = BlackboardValue<float>.Variable("Idle Time", BlackboardScope.Global, 3f * THINK)
            }, "Idle"));
            rig.SetGraph(g.Build());

            rig.Step(THINK, 100);

            Assert.AreEqual("Idle", rig.StateName);
            Assert.AreEqual(0, rig.Context.Blackboard.Count);
            Assert.AreEqual(0, world.Blackboard.Count);

            int changes = 0;
            rig.Context.Blackboard.Changed += _ => changes++;
            rig.Context.Blackboard.Set("x", 1);

            Assert.AreEqual(1, changes);
            Assert.IsTrue(rig.Context.Blackboard.TryGetKey("x", out BlackboardKey<int> key));
            Assert.That(rig.Context.Blackboard.VersionOf(key.Index), Is.GreaterThan(0));
        }

        [Test]
        public void GlobalValue_WorldJoinedAfterTheGraph_ReadsTheWorld()
        {
            var rig = new GraphRig { ThinkInterval = THINK };

            // The routine copies the world's alarm into a number of its own and goes by it
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState read = g.Block(new SetNumberTask
            {
                Variable = "Alarm Seen", Value = BlackboardValue<float>.Variable("Alarm", BlackboardScope.Global)
            }, "Read");
            AgentState alarmed = g.Block(new BranchTask(new CompareCondition("Alarm Seen", NumberComparison.GreaterOrEqual, 1f)),
                "Alarmed?");
            g.Do(routine, read);
            g.Then(read, alarmed);
            g.Then(alarmed, g.Block(new Hold(), "Alarmed"));
            g.OnFail(alarmed, g.Block(new WaitTask(3f * THINK), "Calm"));
            rig.SetGraph(g.Build());
            rig.StepSeconds(0.5f);                      // read (and cached) against the agent's own blackboard
            Assert.AreEqual("Calm", rig.StateName);

            var world = new AIWorld();
            world.Blackboard.Set("Alarm", 1f);
            rig.Agent.SetWorld(world);
            rig.StepSeconds(0.5f);

            Assert.AreEqual("Alarmed", rig.StateName, "The cached key follows the blackboard the scope points at now");
        }
    }
}
