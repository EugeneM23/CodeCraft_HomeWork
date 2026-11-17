using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The Circle decision of step 90, on real agents of one world (handbook §18, §18.3, §18.1, §20b.4 p. 6): its On Tick
    /// asks the target for a place to attack from (Take Attack Slot, Wait Time 0) once per think while it is in charge —
    /// its chain only moves the body — and keeps the agent's place on the ring; the place given, its Attack Slot (not
    /// mine) lets go and the Brain takes the attack; left without a place, the request is taken back at once.
    /// </summary>
    public sealed class CircleDecisionTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private readonly List<Object> _created = new();
        private AIWorld _world;
        private Transform _target;
        private GraphRig _holder;

        [SetUp]
        public void SetUp()
        {
            _world = new AIWorld();
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.AddComponent<AttackSlotCapacity>().Slots = 1;
            _target = go.transform;
            _holder = RigAt(new Vector3(1f, 0f, 0f));
            var take = new TakeAttackSlotTask { Target = TARGET };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 200 && answer == TaskStatus.Running; i++)
            {
                answer = take.Tick(_holder.Context, DT);
                _world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer, "Set-up: the one place is taken");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Circle_ItsOnTickAsksForAPlace_ThePlaceIsFreed_TheBrainTakesTheAttack()
        {
            GraphRig waiter = Waiter(asks: true);
            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Wait Block", 2f), "Every place is taken: it waits");
            Steps(waiter, 2f);
            Assert.AreEqual("Wait Block", waiter.StateName, "Still waiting");

            ReleaseHoldersPlace();

            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Fight Block", 2f),
                "The ask on Circle's On Tick ran all the while: the freed place is its, and the Brain takes the attack");
        }

        [Test]
        public void Circle_NothingOnItsOnTick_NobodyAsks_NoAttack()
        {
            GraphRig waiter = Waiter(asks: false);
            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Wait Block", 2f));

            ReleaseHoldersPlace();
            Steps(waiter, 2f);

            Assert.AreEqual("Wait Block", waiter.StateName, "Its chain does not ask either: the place is nobody's");
        }

        [Test]
        public void Circle_WhileItWaits_ItsPlaceOnTheRingIsKept_LongerThanTheRingForgets()
        {
            GraphRig waiter = Waiter(asks: true);
            Steps(waiter, AttackRing.FORGET * 4f);

            Assert.AreEqual("Wait Block", waiter.StateName, "Set-up: a block that never asks for a place");
            Assert.IsNotNull(_world.Roster.EngagementOf(_target).Ring.PlaceOf(waiter.Agent),
                "The ask on its On Tick keeps its place on the ring");
        }

        [Test]
        public void Circle_LeftWithoutAPlace_TheAskIsTakenBackAtOnce_NotWhenItGoesStale()
        {
            GraphRig waiter = Waiter(asks: true);
            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Wait Block", 2f));
            Steps(waiter, 0.5f);
            Assert.IsTrue(_world.Roster.EngagementOf(_target).Tokens.Pools.Contains(AttackSlots.POOL),
                "Set-up: its request is filed");

            waiter.Agent.Blackboard.Set(TARGET, (Transform)null);     // the target is lost: no decision holds
            waiter.Step(DT);
            Assert.IsNull(waiter.Instance.Decision, "Set-up: the agent left Circle");
            ReleaseHoldersPlace();                                     // well inside STALE_AFTER of its last ask

            Assert.IsFalse(_world.Roster.EngagementOf(_target).Tokens.Holds(waiter.Agent, AttackSlots.POOL),
                "It waits for a place no more: the freed place does not go to it");
        }

        [Test]
        public void Circle_ThePlaceGiven_ItsNotMineConditionLetsGo_TheAskGetsGot()
        {
            GraphRig waiter = Waiter(asks: true);
            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Wait Block", 2f));
            ReleaseHoldersPlace();
            Assert.IsTrue(StepUntil(waiter, () => waiter.StateName == "Fight Block", 2f));

            Assert.IsTrue(_world.Roster.EngagementOf(_target).Tokens.Holds(waiter.Agent, AttackSlots.POOL),
                "The place stays the agent's after Circle has gone");
            ReasonerTask circle = waiter.Instance.Graph.Root.Children.Find(node => node.Reasoner is CircleReasoner).Reasoner;
            using (waiter.Context.AimAt(_target))
                Assert.AreEqual(Truth.No, circle.Condition.Ask(waiter.Context), "Attack Slot (not mine): no");
        }

        // ---------- the rig ----------

        // Brain (Target) → Fight (1, Attack Slot: mine) → Fight Block; Circle (2, Attack Slot: not mine) → Wait Block, a
        // block that never asks for a place; with asks — Take Attack Slot (Wait Time 0) on Circle's On Tick
        private GraphRig Waiter(bool asks)
        {
            GraphRig rig = RigAt(new Vector3(-3f, 0f, 0f));
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState fight = g.Decision(new FightReasoner
            {
                Closer = 100f, LetGoAfter = 100f, Condition = new AttackSlotCondition(AttackSlotIs.Mine)
            }, 1);
            g.Do(fight, g.Block(new WaitTask(100f), "Fight Block"));
            AgentState circle = g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 2);
            g.Do(circle, g.Block(new WaitTask(100f), "Wait Block"));
            if (asks)
                g.OnTick(circle, g.Block(new TakeAttackSlotTask { WaitTime = 0f }));
            rig.SetGraph(g.Build());
            rig.Agent.Blackboard.Set(TARGET, _target);
            return rig;
        }

        private GraphRig RigAt(Vector3 at)
        {
            var rig = new GraphRig(trace: false, world: _world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, _target);
            return rig;
        }

        private void ReleaseHoldersPlace()
        {
            new ReleaseAttackSlotTask().Tick(_holder.Context, DT);
            _world.BeginStep(DT);
        }

        // The world's step, then the agent's — as in the game
        private void Steps(GraphRig rig, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                _world.BeginStep(DT);
                rig.Step(DT);
            }
        }

        private bool StepUntil(GraphRig rig, Func<bool> condition, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                if (condition())
                    return true;

                _world.BeginStep(DT);
                rig.Step(DT);
            }

            return condition();
        }
    }
}
