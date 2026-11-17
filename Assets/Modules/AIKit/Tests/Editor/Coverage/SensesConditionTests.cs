using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The questions of the canvas after step 90 (recheck): a condition with nothing wired in has no data — never a "no"
    /// that NOT turns into "yes", never an empty AND / OR that holds — and the answer kept for a think is asked anew once
    /// a block of the flow has done something, so a Branch in a loop of instant blocks sees what the loop changed; and the
    /// three-valued AND / OR, slack on a Get, the words of a verdict, Attack Slot pointed elsewhere, Is on another object
    /// and why it has no data, one answer per agent per think, the words of the enums, and thinks that allocate nothing.
    /// </summary>
    public sealed class SensesConditionTests
    {
        private const string COUNT = "Count";
        private const float DT = 0.02f;

        private readonly Things _things = new();
        private GraphRig _rig;

        [SetUp]
        public void SetUp() => _rig = new GraphRig(trace: false);

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
        }

        // An object that says things about itself (a status of its own, saying nothing yet)
        private Transform Foe(out GetTests.Status status)
        {
            Transform foe = _things.Make("Foe", new Vector3(2f, 0f, 0f));
            status = foe.gameObject.AddComponent<GetTests.Status>();
            return foe;
        }

        // ---------- nothing wired in ----------

        [Test]
        public void Conditions_NothingWiredIn_IsNoData_EvenUnderIsNot()
        {
            Assert.AreEqual(Truth.NoData, new CompareCondition().Ask(_rig.Context),
                "A Compare whose A wire was taken away: no data, not \"no\"");
            Assert.AreEqual(Truth.NoData, new IsCondition { Of = Whose.WiredObject, Is = IsCheck.IsNot }.Ask(_rig.Context),
                "An Is Not with nothing wired in: no data too, not \"yes\" — a broken wire gives no hidden answer");
            Assert.AreEqual(TaskStatus.Failure, new BranchTask(new AnyCondition()).Tick(_rig.Context, DT),
                "A Branch over an OR whose wires were all taken away goes to False");
        }

        [Test]
        public void AndOr_NoChildWiredIn_NoData()
        {
            Assert.AreEqual(Truth.NoData, new AllCondition().Ask(_rig.Context), "AND of nothing");
            Assert.AreEqual(Truth.NoData, new AnyCondition().Ask(_rig.Context), "OR of nothing");
        }

        [Test]
        public void Decision_AnEmptyOrAsItsCondition_DoesNotHold()
        {
            var facts = new TargetFacts(null, 0f);
            var routine = new DailyRoutineReasoner { Condition = new AnyCondition() };

            Assert.IsFalse(routine.HoldsWithCondition(new ReasonerQuery(_rig.Context, facts, false), out _),
                "A decision whose condition has nothing wired in does not hold as if it had none");
        }

        [Test]
        public void AndOr_ThreeValued_ANoOrAYesDecides_OtherwiseNoDataSpreads()
        {
            AgentContext context = _rig.Context;
            Truth Ask(AgentCondition condition) => condition.Ask(context);
            AnswerCondition Yes() => new(Truth.Yes);
            AnswerCondition No() => new(Truth.No);
            AnswerCondition None() => new(Truth.NoData);

            Assert.AreEqual(Truth.No, Ask(new AllCondition(No(), None())), "AND: a no decides");
            Assert.AreEqual(Truth.NoData, Ask(new AllCondition(Yes(), None())), "AND: yes and no data — no data");
            Assert.AreEqual(Truth.NoData, Ask(new AllCondition(Yes(), null)), "AND: a pin with nothing in it — no data");
            Assert.AreEqual(Truth.Yes, Ask(new AllCondition(Yes(), Yes())));
            Assert.AreEqual(Truth.Yes, Ask(new AnyCondition(Yes(), None())), "OR: a yes decides");
            Assert.AreEqual(Truth.NoData, Ask(new AnyCondition(No(), None())), "OR: no and no data — no data");
            Assert.AreEqual(Truth.No, Ask(new AnyCondition(No(), No())));
            Assert.AreEqual(TaskStatus.Failure, new BranchTask(new AllCondition(Yes(), None())).Tick(context, DT),
                "A Branch over no data goes to False");
        }

        [Test]
        public void Compare_SlackOnAGet_HoldsUntilBeyondTheSlack()
        {
            Transform target = Foe(out GetTests.Status status);
            _rig.Agent.Blackboard.Set("Target", target);
            var low = new CompareCondition(new GetValue(GetParameter.HealthPercent, "Target"), NumberComparison.Less, 30f, slack: 10f);

            Truth Said(float percent)
            {
                status.Say(CharacterStat.HealthPercent, percent);
                return low.Ask(_rig.Context);
            }

            Assert.AreEqual(Truth.Yes, Said(25f), "25 < 30");
            Assert.AreEqual(Truth.Yes, Said(35f), "Held: it lets go only at 30 + 10");
            Assert.AreEqual(Truth.No, Said(41f), "41 ≥ 40: let go");
            Assert.AreEqual(Truth.No, Said(35f), "Not held now: the limit is 30 again");
        }

        [Test]
        public void Decision_ACompareOfAGetWithoutData_TheVerdictNamesTheGet()
        {
            Transform target = Foe(out _);
            _rig.Agent.Blackboard.Set("Target", target);
            var attack = new FightReasoner
            {
                Closer = 10f,
                Condition = new CompareCondition(new GetValue(GetParameter.HealthPercent, "Target"), NumberComparison.Less, 30f)
            };

            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(_rig.Context, new TargetFacts(target, 1f), false),
                out string whyNot));
            Assert.AreEqual("only if health percent of Target < 30 — no data (health percent of Target: no data)", whyNot);
        }

        [Test]
        public void AttackSlot_PointedAtAnotherObject_AsksAboutThatObject_AndItsWords()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            Transform first = Foe(out _);
            first.gameObject.AddComponent<AttackSlotCapacity>().Slots = 2;
            Transform second = Foe(out _);
            second.gameObject.AddComponent<AttackSlotCapacity>().Slots = 2;
            rig.Context.Blackboard.Set("Target", first);
            var take = new TakeAttackSlotTask { Target = "Target" };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 100 && answer == TaskStatus.Running; i++)
            {
                answer = take.Tick(rig.Context, DT);
                world.BeginStep(DT);
            }

            Assume.That(answer, Is.EqualTo(TaskStatus.Success), "Set-up: a place at the first");
            rig.Context.BrainTarget = first;

            Assert.AreEqual(Truth.Yes, new AttackSlotCondition(AttackSlotIs.Mine).Ask(rig.Context), "The Brain's target");
            using (rig.Context.AimAt(second))
            {
                Assert.AreEqual(Truth.No, new AttackSlotCondition(AttackSlotIs.Mine).Ask(rig.Context),
                    "Pointed at the second (a decision asked about it): its place there is asked");
                Assert.AreEqual(Truth.Yes, new AttackSlotCondition(AttackSlotIs.NotMine).Ask(rig.Context));
            }

            Assert.IsNull(world.Roster.FindEngagement(second), "Asking made no engagement round the second");
            Assert.AreEqual("my attack slot (Ranged), I attack first",
                new AttackSlotCondition(AttackSlotIs.MineAndIAttackFirst, "Ranged").Phrase(rig.Context));
            Assert.AreEqual("no attack slot of mine", new AttackSlotCondition(AttackSlotIs.NotMine).Phrase(rig.Context));
        }

        [Test]
        public void Is_TheWiredVariableNowHoldsAnotherObject_ItsOwnSayIsRead_ADestroyedStatusIsNoData()
        {
            Transform a = Foe(out GetTests.Status first);
            Transform b = Foe(out GetTests.Status second);
            first.Say(CharacterState.Blocking, true);
            second.Say(CharacterState.Blocking, false);
            var blocking = new IsCondition(CharacterState.Blocking, "Target");

            _rig.Agent.Blackboard.Set("Target", a);
            Assert.AreEqual(Truth.Yes, blocking.Ask(_rig.Context), "A says it blocks");
            _rig.Agent.Blackboard.Set("Target", b);
            Assert.AreEqual(Truth.No, blocking.Ask(_rig.Context), "Another object in the variable: its own say");
            Object.DestroyImmediate(second);
            Assert.AreEqual(Truth.NoData, blocking.Ask(_rig.Context), "Its status destroyed: no data, no stale answer");
        }

        [Test]
        public void Is_WhyNoData_SaysWhatIsMissing()
        {
            Assert.AreEqual("nothing wired into Object",
                new IsCondition { Of = Whose.WiredObject, State = CharacterState.Moving }.WhyNoData(_rig.Context));

            _rig.Agent.Blackboard.Set("Target", (Transform)null);
            Assert.AreEqual("Target holds nothing", new IsCondition(CharacterState.Moving, "Target").WhyNoData(_rig.Context));

            Transform plain = _things.Make("Plain", Vector3.zero);
            _rig.Agent.Blackboard.Set("Target", plain);
            Assert.AreEqual("Plain says nothing about itself",
                new IsCondition(CharacterState.Moving, "Target").WhyNoData(_rig.Context));

            _rig.Agent.Blackboard.Set("Target", Foe(out _));
            Assert.AreEqual("Target does not say whether it is swimming",
                new IsCondition(CharacterState.Swimming, "Target").WhyNoData(_rig.Context));
        }

        [Test]
        public void Conditions_IsAttackSlotAndCompareOfAGet_ThinkAfterThink_AllocateNothing()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            Transform target = Foe(out GetTests.Status status);
            status.Say(CharacterState.Blocking, false);
            target.gameObject.AddComponent<AttackSlotCapacity>().Slots = 2;
            target.position = new Vector3(3f, 0f, 0f);
            rig.Context.Blackboard.Set("Target", target);
            var take = new TakeAttackSlotTask { Target = "Target" };
            for (int i = 0; i < 100 && take.Tick(rig.Context, DT) == TaskStatus.Running; i++)
                world.BeginStep(DT);
            Assume.That(world.Roster.FindEngagement(target), Is.Not.Null, "Set-up: an engagement round the target");
            var blocking = new IsCondition(CharacterState.Blocking, "Target");
            var slot = new AttackSlotCondition();
            var near = new CompareCondition(new GetValue(GetParameter.Distance, "Target"), NumberComparison.Less, 5f);

            void Think()
            {
                rig.Context.BeginThink();
                using (rig.Context.AimAt(target))
                {
                    blocking.Ask(rig.Context);
                    slot.Ask(rig.Context);
                    near.Ask(rig.Context);
                }

                rig.Context.EndThink();
            }

            for (int i = 0; i < 3; i++)
                Think();

            // Unity's own GC.Alloc counter (GC.GetAllocatedBytesForCurrentThread says 0 in the editor, whatever is made)
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    Think();
            }, Is.Not.AllocatingGCMemory(), "Asked think after think, the questions allocate nothing");
        }

        [Test]
        public void CharacterNodes_Words_EnumsInPlainWords()
        {
            Assert.AreEqual("Target is winding up", new IsCondition(CharacterState.WindingUp, "Target").Phrase(null));
            Assert.AreEqual("I am in combat", new IsCondition(CharacterState.InCombat).Phrase(null));
            Assert.AreEqual("my health percent", new GetValue(GetParameter.HealthPercent).Phrase);
            Assert.AreEqual("angle from my facing of Target", new GetValue(GetParameter.AngleFromMyFacing, "Target").Phrase);
        }

        // ---------- one think, one answer — until a block does something ----------

        [Test]
        public void Condition_OneThinkOneAnswerPerAgent_TheNextThinkAsksAnew()
        {
            var other = new GraphRig(trace: false);
            var shared = new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f);
            _rig.Context.Blackboard.Set(COUNT, 5f);
            other.Context.Blackboard.Set(COUNT, 1f);

            _rig.Context.BeginThink();
            Assert.AreEqual(Truth.Yes, shared.Ask(_rig.Context));
            _rig.Context.Blackboard.Set(COUNT, 0f);                 // changed from outside the flow
            Assert.AreEqual(Truth.Yes, shared.Ask(_rig.Context), "One think, one answer");
            _rig.Context.EndThink();

            other.Context.BeginThink();
            Assert.AreEqual(Truth.No, shared.Ask(other.Context), "Another agent's think: its own answer, not the first's");
            other.Context.EndThink();

            _rig.Context.BeginThink();
            Assert.AreEqual(Truth.No, shared.Ask(_rig.Context), "The next think asks anew");
            _rig.Context.EndThink();
        }

        [Test]
        public void Branch_InALoopOfInstantBlocks_SeesWhatTheLoopChangedInTheSameThink()
        {
            const float LIMIT = 3f;
            var g = new BrainGraphBuilder();
            g.Variable(COUNT, VariableType.Float);
            AgentState always = g.Always();
            AgentState zero = g.Block(new SetNumberTask { Variable = COUNT, Value = 0f });
            AgentState branch = g.Block(new BranchTask(new CompareCondition(COUNT, NumberComparison.Less, LIMIT)));
            AgentState add = g.Block(new AddNumberTask(COUNT, 1f));
            AgentState done = g.Block(new WaitTask(100f), "Done");
            g.Do(always, zero);
            g.Then(zero, branch);
            g.Then(branch, add);
            g.Then(add, branch);
            g.OnFail(branch, done);
            _rig.ThinkInterval = 0f;
            _rig.SetGraph(g.Build());

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Done", 1f), "The loop ends");
            Assert.AreEqual(LIMIT, _rig.Context.Blackboard.Get(COUNT, 0f),
                "Each visit of the Branch sees the number the Add before it wrote: the loop stops at the limit");
        }

        [Test]
        public void Condition_InOneThink_AskedAgainAfterNothingHappened_TheSameAnswer_AfterABlockRan_AskedAnew()
        {
            var asked = new AnswerCondition(Truth.Yes);
            _rig.Context.BeginThink();
            asked.Ask(_rig.Context);
            asked.Ask(_rig.Context);
            Assert.AreEqual(1, asked.Asked, "Asked twice in one think with nothing done between: answered once");

            new AddNumberTask(COUNT, 1f).Tick(_rig.Context, DT);    // a block of the flow did something
            asked.Ask(_rig.Context);
            Assert.AreEqual(2, asked.Asked, "After a block ran: asked anew");
            _rig.Context.EndThink();
        }
    }
}
