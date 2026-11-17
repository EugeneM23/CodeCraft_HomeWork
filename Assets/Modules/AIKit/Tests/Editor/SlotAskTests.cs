using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The visible asking for a place to attack from (step 90, spec 10.3): Take Attack Slot with Wait Time 0 is the
    /// instant ask — it answers at once, keeps its request filed for the next ask and the agent's place on the ring, and a
    /// request still waiting is taken back when the decision whose chain asked leaves; what the blocks that ask by
    /// themselves do is said on their nodes; the graph check finds a place read and asked by nobody; the Brain joins the
    /// scene's group when it starts, so a question never does.
    /// </summary>
    public sealed class SlotAskTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;
        private readonly List<Object> _created = new();
        private AIWorld _world;
        private Transform _target;

        [SetUp]
        public void SetUp()
        {
            _world = new AIWorld();
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.AddComponent<AttackSlotCapacity>().Slots = 1;
            _target = go.transform;
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

        private GraphRig RigAt(Vector3 at, IAIWorld world = null)
        {
            var rig = new GraphRig(trace: false, world: world ?? _world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, _target);
            return rig;
        }

        private bool Holds(GraphRig rig) => _world.Roster.FindEngagement(_target)?.Tokens.Holds(rig.Agent, AttackSlots.POOL) == true;

        // ---------- the instant ask ----------

        [Test]
        public void InstantAsk_AnswersAtOnce_WaitingThenGot_TheRequestStaysFiledBetweenAsks()
        {
            GraphRig rig = RigAt(new Vector3(2f, 0f, 0f));
            var ask = new TakeAttackSlotTask { Target = TARGET, WaitTime = 0f };
            Assert.IsTrue(ask.IsInstant, "Wait Time 0: done at once — it runs on an On Tick pin");

            Assert.AreEqual(TaskStatus.Failure, ask.Tick(rig.Context, DT), "Filed: Waiting, at once");
            _world.BeginStep(DT);
            Assert.IsTrue(Holds(rig), "The request was left filed: the group granted it");
            Assert.AreEqual(TaskStatus.Success, ask.Tick(rig.Context, DT), "Asked again: Got");
            Assert.IsFalse(new TakeAttackSlotTask { WaitTime = 0.2f }.IsInstant, "Waiting a while is not instant");
            Assert.IsFalse(new TakeAttackSlotTask { WaitTime = BlackboardValue<float>.Variable("Patience") }.IsInstant,
                "A number of the blackboard may be anything: not instant");
        }

        [Test]
        public void InstantAsk_KeepsTheAgentsPlaceOnTheRing_TheTimedAskDoesNot()
        {
            GraphRig asking = RigAt(new Vector3(2f, 0f, 0f));
            new TakeAttackSlotTask { Target = TARGET, WaitTime = 0f }.Tick(asking.Context, DT);
            Assert.IsNotNull(_world.Roster.EngagementOf(_target).Ring.PlaceOf(asking.Agent), "On the ring while it waits");

            GraphRig timed = RigAt(new Vector3(-2f, 0f, 0f));
            new TakeAttackSlotTask { Target = TARGET, WaitTime = 0.2f }.Tick(timed.Context, DT);
            Assert.IsNull(_world.Roster.EngagementOf(_target).Ring.PlaceOf(timed.Agent), "A plain ask is only an ask");
        }

        [Test]
        public void InstantAsk_TheDecisionLeaves_AWaitingRequestIsTakenBack_AGrantedPlaceStays()
        {
            GraphRig holder = RigAt(new Vector3(1f, 0f, 0f));
            var take = new TakeAttackSlotTask { Target = TARGET, WaitTime = 0f };
            take.Tick(holder.Context, DT);
            _world.BeginStep(DT);
            Assert.IsTrue(Holds(holder), "Set-up: the one place is the holder's");

            GraphRig waiter = RigAt(new Vector3(0f, 0f, 1.5f));
            Assert.AreEqual(TaskStatus.Failure, take.Tick(waiter.Context, DT), "Every place taken: it waits");
            waiter.Context.NotifyDecisionLeft();                        // the decision whose chain asked leaves
            holder.Context.NotifyDecisionLeft();
            new ReleaseAttackSlotTask().Tick(holder.Context, DT);
            _world.BeginStep(DT);

            Assert.IsFalse(Holds(waiter), "Its waiting request was taken back at once: the freed place is not given to it");
            take.Tick(holder.Context, DT);
            _world.BeginStep(DT);
            holder.Context.NotifyDecisionLeft();
            Assert.IsTrue(Holds(holder), "A place already given stays the agent's when the decision leaves");
        }

        [Test]
        public void InstantAsk_OnADecisionsOnTick_RunsOncePerThink_WhateverBlockOfItsChainIsOn()
        {
            GraphRig rig = RigAt(new Vector3(2f, 0f, 0f));
            rig.ThinkInterval = 0f;
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState wait = g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 2);
            g.Do(wait, g.Block(new WaitTask(100f), "Wait Block"));
            g.OnTick(wait, g.Block(new TakeAttackSlotTask { WaitTime = 0f }));
            g.Do(g.Decision(new FightReasoner { Closer = 100f, Condition = new AttackSlotCondition() }, 1),
                g.Block(new WaitTask(100f), "Fight Block"));
            rig.SetGraph(g.Build());

            for (int i = 0; i < 40 && rig.StateName != "Fight Block"; i++)
            {
                _world.BeginStep(DT);
                rig.Step(DT);
            }

            Assert.AreEqual("Fight Block", rig.StateName, "The ask ran while the Wait Block was on: the place came, the attack");
        }

        // ---------- what the blocks that ask by themselves say ----------

        [Test]
        public void Requirement_TheBlocksThatAskOrTakeAPlace_SayItOnTheirNodes()
        {
            StringAssert.Contains("asks for an attack place", new SquareOffTask().Requirement);
            StringAssert.Contains("asks for an attack place", new CircleTargetTask { WaitForSlot = true }.Requirement);
            Assert.IsNull(new CircleTargetTask { WaitForSlot = false }.Requirement, "One that does not ask says nothing");
            StringAssert.Contains("place on the ring", new ChaseTask().Requirement);
            StringAssert.Contains("place on the ring", new FlankTask().Requirement);
            StringAssert.Contains("goes straight when it attacks first", new FlankTask().Requirement);
            StringAssert.Contains("place on the ring", new StepTask().Requirement);
            StringAssert.Contains("place on the ring", new TakeAttackSlotTask { WaitTime = 0f }.Requirement);
            StringAssert.Contains("gives the attack place back", new DetectionTask { StoreIn = TARGET }.Requirement);
            Assert.IsNull(new TimeSensorTask { StoreIn = "Night" }.Requirement, "The clock holds no place");
            Assert.IsNull(new RadiusSensorTask().Requirement, "A shape in Detection keeps no target of its own");
            Assert.AreEqual("only with a weapon that shoots", new KiteReasoner().Requirement);
            Assert.AreEqual("needs 2 weapons", new ChangeWeaponReasoner().Requirement);
        }

        [Test]
        public void Titles_TheDecisionsAttackAndSwitchWeapon_KeepTheirIds()
        {
            Assert.AreEqual("Attack", NodeTitles.Of(typeof(FightReasoner)));
            Assert.AreEqual("aikit.reasoner.fight", GraphSerializer.TypeIdOf(new FightReasoner()));
            Assert.AreEqual("Switch Weapon", NodeTitles.Of(typeof(ChangeWeaponReasoner)));
            Assert.AreEqual("aikit.reasoner.change-weapon", GraphSerializer.TypeIdOf(new ChangeWeaponReasoner()));
            Assert.AreEqual("Attack Slot", NodeTitles.Of(typeof(AttackSlotCondition)));
            Assert.AreEqual("Is", NodeTitles.Of(typeof(IsCondition)));
            Assert.AreEqual("Get", NodeTitles.Of(typeof(GetValue)));
            Assert.AreEqual("Set Parameter", NodeTitles.Of(typeof(SetParameterTask)));
        }

        // ---------- the graph check ----------

        [Test]
        public void Validator_AnAttackSlotNobodyAsksFor_IsAProblem_AnAskOfItsGroupClearsIt()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState attack = g.Decision(new FightReasoner { Condition = new AttackSlotCondition() }, 1);
            g.Do(attack, g.Block(new WaitTask(1f)));
            AgentState wait = g.Decision(new CircleReasoner(), 2);
            AgentState waiting = g.Block(new WaitTask(1f));
            g.Do(wait, waiting);
            var issues = new List<GraphIssue>();

            ConditionChecks.Check(g.Build(), issues);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot), "Nobody asks: it never comes");

            g.OnTick(wait, g.Block(new TakeAttackSlotTask { WaitTime = 0f, Pool = "Melee" }));
            issues.Clear();
            ConditionChecks.Check(g.Build(), issues);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot), "Another group is asked for");

            // Square Off takes time: on an On Tick pin it would never run (it is no asker there) — in the chain it is one
            g.Then(waiting, g.Block(new SquareOffTask()));
            issues.Clear();
            ConditionChecks.Check(g.Build(), issues);
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot), "Square Off asks the common group");
        }

        [Test]
        public void Validator_AnIsOfTheWiredObjectWithNothingWired_IsAProblem()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Decision(new FightReasoner { Condition = new IsCondition { Of = Whose.WiredObject } }, 1),
                g.Block(new WaitTask(1f)));
            var issues = new List<GraphIssue>();

            ConditionChecks.Check(g.Build(), issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.UnwiredValue), string.Join("\n", issues));
        }

        // ---------- the group is joined when the Brain starts, never by a question ----------

        [Test]
        public void Brain_Starts_TheCharacterJoinsTheScenesGroup_AQuestionJoinsNothing()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.gameObject.SetActive(true);
            Assert.AreEqual(Truth.Yes, new AttackSlotCondition().Ask(Aimed(rig)), "No group yet: nothing limits the attack");
            Assert.IsNull(rig.Agent.World, "The question joined nothing");

            rig.SetGraph(new BrainGraphBuilder().Build());
            rig.Step(DT);

            Assert.IsNotNull(rig.Agent.World, "The Brain started: the character is in the scene's group");
            rig.Agent.SetWorld(null);
        }

        private AgentContext Aimed(GraphRig rig)
        {
            rig.Context.BrainTarget = _target;
            return rig.Context;
        }
    }
}
