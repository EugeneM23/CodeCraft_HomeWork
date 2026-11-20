using Atomic.Entities;
using NUnit.Framework;

namespace AIKit.Entities.Tests
{
    /// <summary>The world on an entity: its values come from the generated API, its tick steps the world and the agents.</summary>
    public sealed class AIWorldInstallerTests
    {
        private sealed class CountingAgent : IAIAgent
        {
            public int Steps;
            public AICharacter Character => null;
            public void Step(float dt) => Steps++;
        }

        [Test]
        public void Install_TheOwnerHasTheTagTheListAndTheStep()
        {
            var entity = new Entity("Level");
            var world = new AIWorld(7);

            AIAgentList agents = AIWorldInstaller.Install(entity, world);

            Assert.IsTrue(entity.HasAIWorldOwnerTag());
            Assert.AreSame(agents, entity.GetWorldAgentList());
            Assert.AreSame(world, agents.World);
        }

        [Test]
        public void EntityTick_StepsEveryAgentOnce()
        {
            var entity = new Entity("Level");
            AIAgentList agents = AIWorldInstaller.Install(entity, new AIWorld());
            var a = new CountingAgent();
            var b = new CountingAgent();
            agents.Add(a);
            agents.Add(b);

            entity.Init();
            entity.Enable();
            entity.Tick(0.02f);

            Assert.AreEqual(1, a.Steps);
            Assert.AreEqual(1, b.Steps);
        }
    }
}
