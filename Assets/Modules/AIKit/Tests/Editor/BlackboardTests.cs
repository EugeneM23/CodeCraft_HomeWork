using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    public class BlackboardTests
    {
        [Test]
        public void Declare_SameNameTwice_ReturnsTheSameSlot()
        {
            var blackboard = new Blackboard();

            BlackboardKey<float> first = blackboard.Declare("Range", 2f);
            BlackboardKey<float> second = blackboard.Declare("Range", 5f);

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreEqual(2f, blackboard.Get(second), "A second declaration must not overwrite the value");
        }

        [Test]
        public void Declare_SameNameOtherType_Throws()
        {
            var blackboard = new Blackboard();
            blackboard.Declare("Range", 2f);

            Assert.Throws<System.InvalidOperationException>(() => blackboard.Declare("Range", true));
        }

        [Test]
        public void Set_NewValue_RaisesChangedOnce()
        {
            var blackboard = new Blackboard();
            BlackboardKey<int> key = blackboard.Declare("Count", 0);
            int changes = 0;
            blackboard.Changed += _ => changes++;

            blackboard.Set(key, 3);
            blackboard.Set(key, 3);                 // the same value is not a change

            Assert.AreEqual(3, blackboard.Get(key));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Get_UnknownName_GivesFallbackAndDeclaresNothing()
        {
            var blackboard = new Blackboard();

            float value = blackboard.Get("Speed", 7f);

            Assert.AreEqual(7f, value);
            Assert.IsFalse(blackboard.Has("Speed"), "Reading must not change the blackboard");
            Assert.AreEqual(0, blackboard.Count);
        }

        [Test]
        public void Value_Constant_IgnoresBlackboard()
        {
            var rig = new GraphRig();
            BlackboardValue<float> value = 2.5f;

            Assert.AreEqual(2.5f, value.Get(rig.Context));
            Assert.IsFalse(value.UsesVariable);
        }

        [Test]
        public void Value_Variable_ReadsAndWritesItsScope()
        {
            var rig = new GraphRig();
            BlackboardValue<float> value = BlackboardValue<float>.Variable("AttackRange");
            rig.Context.Blackboard.Set("AttackRange", 3f);

            Assert.AreEqual(3f, value.Get(rig.Context));

            value.Set(rig.Context, 4f);

            Assert.AreEqual(4f, rig.Context.Blackboard.Get<float>("AttackRange"));
        }

        [Test]
        public void Variable_GlobalScope_IsSharedByAgents()
        {
            var world = new AIWorld();
            var first = new GraphRig(world: world);
            var second = new GraphRig(world: world);

            first.Context.SetVariable(BlackboardScope.Global, "Alert", 1f);

            Assert.AreEqual(1f, second.Context.GetVariable(BlackboardScope.Global, "Alert", 0f));
            Assert.AreEqual(0f, second.Context.GetVariable(BlackboardScope.Agent, "Alert", 0f),
                "An agent scope must not see another agent's facts");
        }

        [Test]
        public void Variable_SquadScope_Alone_IsDetachedAndWarnsOnce()
        {
            var rig = new GraphRig();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Squad scope but has no squad"));

            rig.Context.SetVariable(BlackboardScope.Squad, "Order", 2f);
            rig.Context.SetVariable(BlackboardScope.Squad, "Target", 1f);

            Assert.AreEqual(2f, rig.Context.GetVariable(BlackboardScope.Squad, "Order", 0f));
            Assert.AreEqual(0f, rig.Context.GetVariable(BlackboardScope.Agent, "Order", 0f),
                "A lone agent's squad scope must not quietly be its own blackboard");
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Vector_Variable_RoundTrips()
        {
            var blackboard = new Blackboard();

            blackboard.Set("Home", new Vector3(1f, 2f, 3f));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), blackboard.Get<Vector3>("Home"));
        }
    }
}
