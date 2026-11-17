using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests
{
    /// <summary>
    /// The decisions and the eyes of a squad's command graph (step 88; handbook §21 — the coordinator decides for the
    /// group; §21.2 — no flicker; invariant 11 — a think allocates nothing while nobody looks): Surround is not taken
    /// again for Again After seconds after it let go, whatever other decision let go meanwhile; Gather says why it does
    /// not hold without a new string every think; Hold Line is taken within Contact of a group and let go beyond Let Go
    /// After, never for a lone foe; Squad Sight writes only its kinds, once every Interval, comparing tags without a new
    /// string; Follow Squad lets go Led For after the squad stops leading; Keep Formation says on its node that it strikes
    /// only on a turn its squad gives.
    /// </summary>
    public sealed class WorldSquadDecisionTests
    {
        private const string TARGET = "Target";

        private readonly List<Object> _created = new();

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

        // ---------- Surround ----------

        [Test]
        public void Surround_NotTakenAgainBeforeAgainAfter_EvenIfAnotherDecisionLeftMeanwhile()
        {
            Squad squad = SquadInALine(10, out _);
            var surround = new SurroundDecision { Within = 5f, AgainAfter = 3f };
            Transform foe = Thing("Foe", new Vector3(0f, 0f, 3f));             // alone, 3 m from the nearest member

            squad.MarkLeft(surround);                                        // the foe broke out at 0 s
            squad.Begin(0.5f);
            squad.MarkLeft(new AdvanceDecision());                           // the pursuit lost sight of it at 0.5 s
            squad.Begin(0.7f);

            bool holds = surround.Holds(new SquadQuery(squad, foe, false, true), out string whyNot);
            Assert.IsFalse(holds, "1.2 s after it broke out: not surrounded again yet");
            Assert.AreEqual("it broke out 1.2 s ago", whyNot);

            squad.Begin(1.9f);
            Assert.IsTrue(surround.Holds(new SquadQuery(squad, foe, false, true), out _),
                $"{surround.AgainAfter} s after it broke out: surrounded again");
        }

        // ---------- Gather ----------

        [Test]
        public void GatherDecision_HeldByItsCondition_Called_ItselfAllocatesNothing()
        {
            Squad squad = SquadInALine(1, out _);
            var context = new AgentContext(null, new Blackboard()) { Leads = squad };
            context.Blackboard.Set("Called", false);
            var gather = new GatherDecision { Condition = new CompareCondition("Called", VariableType.Bool) };
            var asked = new ReasonerQuery(context, TargetFacts.None, false);
            Assert.IsFalse(gather.HoldsWithCondition(asked, out _), "Not called: its condition does not hold");
            context.Blackboard.Set("Called", true);
            Assert.IsTrue(gather.HoldsWithCondition(asked, out _), "Called: it holds");

            // The decision itself (its condition aside) holds always and allocates nothing while nobody looks
            var query = new SquadQuery(squad, null, false, false, context);
            bool held = true;
            TestDelegate thinks = () =>
            {
                for (int i = 0; i < 1000; i++)
                    held &= gather.Holds(query, out _);
            };
            thinks();                                                       // the first call of a method may allocate once

            // (GC.GetAllocatedBytesForCurrentThread answers 0 in this editor: Unity's own constraint measures)
            Assert.That(thinks, Is.Not.AllocatingGCMemory(), "A thousand thinks while nobody looks");
            Assert.IsTrue(held);
        }

        // ---------- Hold Line ----------

        [Test]
        public void HoldLineDecision_TakenWithin4m_LetGoBeyond7m_NeverForALoneFoe()
        {
            Squad squad = SquadInALine(10, out List<GraphRig> members);
            var decision = new HoldLineDecision();
            Assert.AreEqual(4f, decision.Contact, "Set-up: the defaults this test counts with");
            Assert.AreEqual(7f, decision.LetGoAfter);
            Transform foe = Thing("Foe", new Vector3(0f, 0f, 5f));          // 5 m from the nearest member
            Transform second = Thing("Second Foe", new Vector3(3f, 0f, 5f));  // 3 m from it: a group
            members[0].Context.BrainTarget = foe;
            squad.Saw(members[0].Context, foe);
            squad.Saw(members[1].Context, second);
            squad.Begin(0.02f);
            Assert.AreEqual(2, squad.Foes.Count, "Set-up: the squad sees two foes");

            Assert.IsFalse(decision.Holds(new SquadQuery(squad, foe, false, true), out string whyNot), "5 m: not yet");
            Assert.AreEqual("foes 5.0 m, needs 4.0 or closer", whyNot);
            Assert.IsTrue(decision.Holds(new SquadQuery(squad, foe, true, true), out _), "5 m while holding: still held");

            foe.position = new Vector3(0f, 0f, 7.5f);
            second.position = new Vector3(3f, 0f, 7.5f);
            Assert.IsFalse(decision.Holds(new SquadQuery(squad, foe, true, true), out _), "7.5 m: let go");

            foe.position = new Vector3(0f, 0f, 3f);
            squad.Saw(members[0].Context, foe);
            squad.Saw(members[1].Context, foe);                               // the second foe is seen no more
            squad.Begin(0.02f);
            Assert.AreEqual(1, squad.Foes.Count, "Set-up: one foe left in sight");
            Assert.IsFalse(decision.Holds(new SquadQuery(squad, foe, false, true), out whyNot), "A lone foe: never");
            Assert.AreEqual("the foe is alone", whyNot);
        }

        // ---------- Squad Sight ----------

        [Test]
        public void SquadSight_WritesOnlyItsKinds_OnceEveryInterval()
        {
            Squad squad = SquadInALine(1, out List<GraphRig> members);
            Transform first = Thing("First", new Vector3(0f, 0f, 5f));
            first.gameObject.tag = "Finish";
            Transform second = Thing("Second", new Vector3(0f, 0f, 4f));
            second.gameObject.tag = "Respawn";
            var context = new AgentContext(null, new Blackboard()) { Leads = squad };
            Sees(squad, members[0], first);

            var sight = new SquadSightTask { StoreIn = TARGET, Tag = "Respawn", Interval = 0f };
            sight.Tick(context, 0.02f);
            Assert.IsNull(context.Blackboard.Get<Transform>(TARGET), "The focus is of another kind: nothing written");

            sight.AlsoTag = "Finish";
            sight.Tick(context, 0.02f);
            Assert.AreSame(first, context.Blackboard.Get<Transform>(TARGET), "Its second kind: written");

            // Steps of an eighth of a second: its Interval of half a second is four of them, exactly
            const float STEP = 0.125f;
            var timed = new SquadSightTask { StoreIn = TARGET, Tag = "", Interval = 0.5f };
            context.Blackboard.Set(TARGET, (Transform)null);
            timed.Tick(context, STEP);
            Assert.AreSame(first, context.Blackboard.Get<Transform>(TARGET), "Any kind: the focus, at its first look");
            Sees(squad, members[0], second);                                // the focus changes right after it
            Assert.AreSame(second, squad.Target, "Set-up: a new focus");
            for (int i = 1; i < 4; i++)
            {
                timed.Tick(context, STEP);
                Assert.AreSame(first, context.Blackboard.Get<Transform>(TARGET), $"{i * STEP} s on: not before its next look");
            }

            timed.Tick(context, STEP);
            Assert.AreSame(second, context.Blackboard.Get<Transform>(TARGET), "At its next look, Interval after the last");
        }

        [Test]
        public void SquadSight_ATagSet_ALookAllocatesNothing()
        {
            Squad squad = SquadInALine(1, out List<GraphRig> members);
            Transform foe = Thing("Foe", new Vector3(0f, 0f, 5f));
            foe.gameObject.tag = "Finish";
            Sees(squad, members[0], foe);
            var context = new AgentContext(null, new Blackboard()) { Leads = squad };
            var sight = new SquadSightTask { StoreIn = TARGET, Tag = "Player", AlsoTag = "Finish", Interval = 0f };
            sight.Tick(context, 0.02f);
            Assert.AreSame(foe, context.Blackboard.Get<Transform>(TARGET), "Set-up: seen by its second kind");
            TestDelegate looks = () =>
            {
                for (int i = 0; i < 1000; i++)
                    sight.Tick(context, 0.02f);
            };
            looks();                                                        // the first call of a method may allocate once

            Assert.That(looks, Is.Not.AllocatingGCMemory(), "A thousand looks comparing tags");
            Assert.AreSame(foe, context.Blackboard.Get<Transform>(TARGET));
        }

        // ---------- Follow Squad ----------

        [Test]
        public void FollowSquad_TheSquadLeadsNoMore_LetsGoWithinLedFor()
        {
            Squad squad = SquadInALine(1, out List<GraphRig> members);
            AgentContext member = members[0].Context;
            var follow = new FollowSquadReasoner();
            squad.Led();                                                    // a block led the formation at 0 s
            float ledAt = squad.Now;

            while (squad.Now - ledAt < Squad.LED_FOR - 0.01f)
            {
                Assert.IsTrue(squad.IsLed, $"{squad.Now - ledAt:0.00} s after it last led: still led");
                Assert.IsTrue(follow.Holds(new ReasonerQuery(member, TargetFacts.None, true), out _));
                squad.Begin(0.02f);
            }

            squad.Begin(0.04f);
            Assert.IsFalse(squad.IsLed, $"More than {Squad.LED_FOR} s: led no more");
            Assert.IsFalse(follow.Holds(new ReasonerQuery(member, TargetFacts.None, true), out string whyNot));
            Assert.AreEqual("the squad leads no formation", whyNot);
        }

        // ---------- Keep Formation ----------

        [Test]
        public void KeepFormation_SaysOnItsNode_ItStrikesOnlyOnATurnFromItsSquad()
        {
            string requirement = new KeepFormationTask().Requirement;

            Assert.IsNotNull(requirement, "A member within reach may stand without striking: its node says why");
            StringAssert.Contains("turn", requirement);
            StringAssert.Contains("Strikers Per Foe", requirement);
            StringAssert.Contains("rest", requirement);
        }

        // ---------- helpers ----------

        // A squad of members standing in a line along x from the origin, 1.8 m apart (its banner at the origin)
        private Squad SquadInALine(int count, out List<GraphRig> members)
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            members = new List<GraphRig>();
            for (int i = 0; i < count; i++)
            {
                var rig = new GraphRig(trace: false);
                rig.WakeUpAt(new Vector3(i * 1.8f, 0f, 0f));
                squad.Join(rig.Context);
                members.Add(rig);
            }

            squad.Begin(0f);
            squad.End();
            return squad;
        }

        // The member sees the foe: its Brain hands it in, the squad takes it in at its next step
        private static void Sees(Squad squad, GraphRig member, Transform foe)
        {
            squad.Saw(member.Context, foe);
            squad.Begin(0.02f);
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }
    }
}
