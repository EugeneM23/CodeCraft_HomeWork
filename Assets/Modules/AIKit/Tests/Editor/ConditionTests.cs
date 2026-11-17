using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The conditions of step 90 (C7, C13): Attack Slot — a pure question about the agent's place to attack the Brain's
    /// target from (it never makes an engagement, never joins the agent anywhere) — and Is — a state of a character as it
    /// says it, with "no data" an answer of its own; and a decision with a wired condition: it holds only when the
    /// condition says yes, and its verdict says which condition did not.
    /// </summary>
    public sealed class ConditionTests
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
            go.AddComponent<AttackSlotCapacity>().Slots = 2;
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

        private GraphRig RigAt(Vector3 at, IAIWorld world)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, _target);
            return rig;
        }

        // The place given: asked until the group answers
        private void Take(GraphRig rig, string pool = "")
        {
            var take = new TakeAttackSlotTask { Target = TARGET, Pool = pool };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 100 && answer == TaskStatus.Running; i++)
            {
                answer = take.Tick(rig.Context, DT);
                _world.BeginStep(DT);
            }

            Assert.AreEqual(TaskStatus.Success, answer, "Set-up: the place is given");
        }

        private Truth Ask(AgentCondition condition, GraphRig rig)
        {
            using (rig.Context.AimAt(_target))
                return condition.Ask(rig.Context);
        }

        // ---------- Attack Slot ----------

        [Test]
        public void AttackSlot_IsAPureQuestion_NoEngagementIsMade_NobodyJoinsAnything()
        {
            GraphRig alone = RigAt(Vector3.left, null);
            GraphRig grouped = RigAt(Vector3.right, _world);

            Assert.AreEqual(Truth.Yes, Ask(new AttackSlotCondition(AttackSlotIs.Mine), alone),
                "No group: nothing limits the attack");
            Assert.IsNull(alone.Agent.World, "Asking joined it to no group");

            Assert.AreEqual(Truth.No, Ask(new AttackSlotCondition(AttackSlotIs.Mine), grouped), "A group, no place asked");
            Assert.AreEqual(Truth.Yes, Ask(new AttackSlotCondition(AttackSlotIs.NotMine), grouped));
            Assert.IsNull(_world.Roster.FindEngagement(_target), "The question made no engagement round the target");
        }

        [Test]
        public void AttackSlot_EveryIs_TheFirstHolderAttacksFirst_TheSecondHasSomeoneBeforeIt()
        {
            GraphRig first = RigAt(new Vector3(-2f, 0f, 0f), _world);
            GraphRig second = RigAt(new Vector3(2f, 0f, 0f), _world);
            GraphRig waiting = RigAt(new Vector3(0f, 0f, 3f), _world);
            Take(first);
            Take(second);

            foreach ((GraphRig rig, bool mine, bool attacksFirst) in new[]
                     { (first, true, true), (second, true, false), (waiting, false, false) })
            {
                string who = rig == first ? "the first" : rig == second ? "the second" : "the one waiting";
                Assert.AreEqual(mine ? Truth.Yes : Truth.No, Ask(new AttackSlotCondition(AttackSlotIs.Mine), rig), who);
                Assert.AreEqual(mine ? Truth.No : Truth.Yes, Ask(new AttackSlotCondition(AttackSlotIs.NotMine), rig), who);
                Assert.AreEqual(mine && attacksFirst ? Truth.Yes : Truth.No,
                    Ask(new AttackSlotCondition(AttackSlotIs.MineAndIAttackFirst), rig), who);
                Assert.AreEqual(mine && !attacksFirst ? Truth.Yes : Truth.No,
                    Ask(new AttackSlotCondition(AttackSlotIs.MineAndSomeoneAttacksFirst), rig), who);
            }
        }

        [Test]
        public void AttackSlot_AGroupOfItsOwn_AsksThatGroupOnly()
        {
            const string RANGED = "Ranged";
            GraphRig archer = RigAt(new Vector3(-2f, 0f, 0f), _world);
            Take(archer, RANGED);

            Assert.AreEqual(Truth.Yes, Ask(new AttackSlotCondition(AttackSlotIs.Mine, RANGED), archer));
            Assert.AreEqual(Truth.No, Ask(new AttackSlotCondition(AttackSlotIs.Mine), archer), "Not a place of the common group");
        }

        [Test]
        public void AttackSlot_NoTargetToAskAbout_NoData()
        {
            GraphRig rig = RigAt(Vector3.zero, _world);

            Assert.AreEqual(Truth.NoData, new AttackSlotCondition().Ask(rig.Context), "Not pointed anywhere, no Brain's target");

            rig.Context.BrainTarget = _target;                       // a Branch of a decision's chain asks
            Assert.AreEqual(Truth.No, new AttackSlotCondition().Ask(rig.Context), "The Brain's target of the chain");
        }

        // ---------- Is ----------

        [Test]
        public void Is_EveryStateTheObjectSays_IsItsAnswer_OneItDoesNotSay_IsNoData()
        {
            GraphRig rig = RigAt(Vector3.zero, null);
            GetTests.Status status = _target.gameObject.AddComponent<GetTests.Status>();
            foreach (CharacterState state in System.Enum.GetValues(typeof(CharacterState)))
            {
                var asked = new IsCondition(state, TARGET);
                Assert.AreEqual(Truth.NoData, asked.Ask(rig.Context), $"{state}: it does not say — no data");

                status.Say(state, true);
                Assert.AreEqual(Truth.Yes, asked.Ask(rig.Context), state.ToString());
                status.Say(state, false);
                Assert.AreEqual(Truth.No, asked.Ask(rig.Context), state.ToString());
                Assert.AreEqual(Truth.Yes, new IsCondition(state, TARGET) { Is = IsCheck.IsNot }.Ask(rig.Context),
                    $"{state}: Is Not — yes");
                status.Forget(state);
                Assert.AreEqual(Truth.NoData, new IsCondition(state, TARGET) { Is = IsCheck.IsNot }.Ask(rig.Context),
                    "Is Not: no data stays no data");
            }
        }

        [Test]
        public void Is_OfMe_AsksTheAgentsOwnStatus()
        {
            GraphRig rig = RigAt(Vector3.zero, null);

            Assert.AreEqual(Truth.No, new IsCondition(CharacterState.Moving).Ask(rig.Context));
            rig.Actuator.Move(Vector3.right, 1f);
            Assert.AreEqual(Truth.Yes, new IsCondition(CharacterState.Moving).Ask(rig.Context));
            Assert.AreEqual(Truth.NoData, new IsCondition(CharacterState.Swimming).Ask(rig.Context), "It cannot tell");
        }

        [Test]
        public void Is_OfTheWiredObject_NothingWired_OrNothingInIt_OrNoStatus_IsNoData_NeverMe()
        {
            GraphRig rig = RigAt(Vector3.zero, null);
            rig.Actuator.Move(Vector3.right, 1f);                    // I move

            Assert.AreEqual(Truth.NoData, new IsCondition { Of = Whose.WiredObject, State = CharacterState.Moving }.Ask(rig.Context),
                "Nothing wired into Object: no data, not my own state");
            Assert.AreEqual(Truth.NoData, new IsCondition(CharacterState.Moving, TARGET).Ask(rig.Context),
                "The target says nothing about itself");
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.AreEqual(Truth.NoData, new IsCondition(CharacterState.Moving, TARGET).Ask(rig.Context), "Nothing in the variable");
        }

        // ---------- a decision with a wired condition ----------

        [Test]
        public void Decision_AWiredCondition_HoldsOnlyWhenItSaysYes_TheVerdictNamesIt()
        {
            GraphRig rig = RigAt(Vector3.zero, null);
            GetTests.Status status = _target.gameObject.AddComponent<GetTests.Status>();
            var attack = new FightReasoner { Closer = 10f, Condition = new IsCondition(CharacterState.Blocking, TARGET) };
            var facts = new TargetFacts(_target, 1f);

            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out string noData));
            Assert.AreEqual("only if Target is blocking — no data (Target does not say whether it is blocking)", noData);

            status.Say(CharacterState.Blocking, false);
            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out string no));
            Assert.AreEqual("only if Target is blocking — no", no);

            status.Say(CharacterState.Blocking, true);
            Assert.IsTrue(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out string none));
            Assert.IsNull(none);

            attack.Closer = 0.5f;
            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out string far),
                "Its own settings first: the condition is not even asked");
            StringAssert.StartsWith("target 1.0 m", far);

            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false, explains: false), out string quiet));
            Assert.IsNull(quiet, "Nobody looks: no words are made");
        }

        [Test]
        public void Brain_ADecisionWithAWiredCondition_IsTakenOnlyWhileItSaysYes()
        {
            GraphRig rig = RigAt(Vector3.zero, null);
            rig.ThinkInterval = 0f;
            GetTests.Status status = _target.gameObject.AddComponent<GetTests.Status>();
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new FightReasoner { Closer = 10f, LetGoAfter = 10f, Condition = new IsCondition(CharacterState.Recovering, TARGET) }, 1),
                g.Block(new WaitTask(100f), "Punish"));
            g.Do(g.Decision(new WatchReasoner(), 2), g.Block(new WaitTask(100f), "Watch Block"));
            rig.SetGraph(g.Build());

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Watch Block", 1f), "It does not say: the attack is not taken");
            status.Say(CharacterState.Recovering, true);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Punish", 1f), "It recovers: the attack is taken");
            status.Say(CharacterState.Recovering, false);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Watch Block", 1f), "No longer: the Brain lets it go");
        }
    }
}
