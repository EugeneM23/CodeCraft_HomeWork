using System;
using AIKit;
using NUnit.Framework;

namespace Game.AI.Tests
{
    /// <summary>The level's agent list: an agent leaving during the step (it died) does not make the next one miss its step.</summary>
    public sealed class AIAgentListTests
    {
        private sealed class CountingAgent : IAIAgent
        {
            public int Steps;
            public Action OnStep;

            public AICharacter Character => null;

            public void Step(float dt)
            {
                Steps++;
                OnStep?.Invoke();
            }
        }

        [Test]
        public void AnAgentLeavingDuringTheStep_TheRestAreSteppedOnce()
        {
            var list = new AIAgentList(null);
            var a = new CountingAgent();
            var b = new CountingAgent();
            var c = new CountingAgent();
            list.Add(a);
            list.Add(b);
            list.Add(c);
            b.OnStep = () => list.Remove(b);

            list.Step(0.02f);

            Assert.AreEqual(1, a.Steps);
            Assert.AreEqual(1, b.Steps);
            Assert.AreEqual(1, c.Steps, "the one after the leaver is not skipped");
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void AnEarlierAgentLeavingDuringTheStep_NobodyIsSteppedTwice()
        {
            var list = new AIAgentList(null);
            var a = new CountingAgent();
            var b = new CountingAgent();
            var c = new CountingAgent();
            list.Add(a);
            list.Add(b);
            list.Add(c);
            b.OnStep = () => list.Remove(a);

            list.Step(0.02f);

            Assert.AreEqual(1, a.Steps);
            Assert.AreEqual(1, b.Steps, "not stepped again after the shift");
            Assert.AreEqual(1, c.Steps);
        }
    }
}
