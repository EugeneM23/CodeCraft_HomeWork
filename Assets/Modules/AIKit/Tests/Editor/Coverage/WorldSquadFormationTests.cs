using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The formation of a squad (step 88; handbook §20a — slots round a point of the squad's own; §20a.2 — holes are
    /// filled from the back, the formation as fast as its members keep up; §21.2 — no flicker between targets; §18 —
    /// turns to strike, not everybody at once): the focus is kept Focus Hold seconds and given up only for a foe Focus
    /// Better times nearer, and dropped Sight Memory after nobody sees it; at most Strikers Per Foe have a turn at one foe,
    /// a member rests after its own blow, a turn not given back runs out, a foe gone takes its turns with it, the rear rank
    /// strikes only when Front Only is off; the pace follows how many stand in their slots; a turn wider than About Face is
    /// an about-face; two fallen in front are replaced from behind and a newcomer takes the back; other Files deal the ranks
    /// anew; March To Target stops its front Stop At short of the target, no faster than its speed times the pace; a member
    /// settled on its slot stays until the slot is Settle Off away, then makes up the distance at Gain.
    /// </summary>
    public sealed class WorldSquadFormationTests
    {
        private const float STEP = 0.125f;                                  // a binary fraction: times add up exactly

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

        // ---------- the focus ----------

        [Test]
        public void Squad_Focus_KeptTwoSeconds_GivenUpOnlyForOneAndAHalfTimesNearer_DroppedHalfASecondAfterItIsUnseen()
        {
            Assert.AreEqual(2f, Squad.FOCUS_HOLD, "Set-up: the numbers this test counts with");
            Assert.AreEqual(1.5f, Squad.FOCUS_BETTER);
            Assert.AreEqual(0.5f, Squad.SIGHT_MEMORY);

            // B at 5 m: 5 × 1.5 = 7.5 < 10 — clearly nearer: taken once A has been the focus for two seconds
            Squad squad = TwoWatchers(out GraphRig first, out GraphRig second);
            Transform a = Thing("A", new Vector3(0f, 0f, 10f));
            Transform b = Thing("B", new Vector3(0f, 0f, 5f));
            var focus = Watch(squad, first, a, second, b, aSeenUntil: float.MaxValue, until: 3f);
            Assert.That(focus.Where(at => at.Now < Squad.FOCUS_HOLD).Select(at => at.Target), Is.All.SameAs(a),
                "A, the first seen, is kept for two seconds though B is nearer");
            Assert.AreSame(b, focus.First(at => at.Now >= Squad.FOCUS_HOLD).Target, "Two seconds on: the nearer B");

            // B at 7 m: 7 × 1.5 = 10.5 > 10 — not clearly nearer: A is kept
            b.position = new Vector3(0f, 0f, 7f);
            squad = TwoWatchers(out first, out second);
            focus = Watch(squad, first, a, second, b, aSeenUntil: float.MaxValue, until: 4f);
            Assert.That(focus.Select(at => at.Target), Is.All.SameAs(a), "Not one and a half times nearer: A stays");

            // A seen no more after 1 s: it is dropped Sight Memory after it was last seen, and B is the focus
            squad = TwoWatchers(out first, out second);
            focus = Watch(squad, first, a, second, b, aSeenUntil: 1f, until: 3f);
            float lastSeen = focus.Where(at => at.Now <= 1f).Max(at => at.Now) - STEP;
            Assert.That(focus.Where(at => at.Now <= lastSeen + Squad.SIGHT_MEMORY).Select(at => at.Target),
                Is.All.SameAs(a), "Remembered for half a second");
            Assert.AreSame(b, focus.First(at => at.Now > lastSeen + Squad.SIGHT_MEMORY).Target, "…then the one seen");
        }

        // ---------- turns to strike ----------

        [Test]
        public void Squad_MayStrike_StrikersPerFoe_RestAfterItsOwnBlow_ATurnExpiresAfterFourSeconds()
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            squad.Configure(3, 1.8f, 2f, 2);                                // three a rank, two strikers at a foe
            GraphRig m1 = Member(squad, new Vector3(-1.8f, 0f, 0f));
            GraphRig m2 = Member(squad, new Vector3(0f, 0f, 0f));
            GraphRig m3 = Member(squad, new Vector3(1.8f, 0f, 0f));
            GraphRig rear = Member(squad, new Vector3(0f, 0f, -2f));
            squad.Begin(0f);
            squad.End();
            Transform foe = Thing("Foe", new Vector3(0f, 0f, 3f));
            Assert.IsTrue(new[] { m1, m2, m3 }.All(m => Front(squad, m)) && !Front(squad, rear), "Set-up: three in front");

            Assert.IsTrue(squad.MayStrike(m1.Context, foe));
            Assert.IsTrue(squad.MayStrike(m2.Context, foe));
            Assert.IsFalse(squad.MayStrike(m3.Context, foe), "Two strike that foe already");
            Assert.AreEqual(2, squad.StrikersAt(foe));
            Assert.IsTrue(squad.MayStrike(m1.Context, foe), "One with a turn keeps it");

            squad.EndStrike(m1.Context);
            float endedAt = squad.Now;
            Assert.IsTrue(squad.MayStrike(m3.Context, foe), "A turn given back goes to the next one");
            float m3TurnAt = squad.Now;
            squad.EndStrike(m2.Context);
            Assert.IsFalse(squad.MayStrike(m1.Context, foe), "Right after its own blow: it rests");
            StepTo(squad, endedAt + Squad.STRIKE_REST - STEP);
            Assert.IsFalse(squad.MayStrike(m1.Context, foe), $"Still resting {squad.Now - endedAt:0.###} s after its blow");
            StepTo(squad, endedAt + Squad.STRIKE_REST + STEP);
            Assert.IsTrue(squad.MayStrike(m1.Context, foe), $"{Squad.STRIKE_REST} s of rest and a turn free: it strikes again");
            Assert.AreEqual(2, squad.StrikersAt(foe), "Set-up: m1 and m3");

            StepTo(squad, m3TurnAt + Squad.STRIKE_TURN);
            Assert.AreEqual(2, squad.StrikersAt(foe), "Turns held no more than four seconds stay");
            StepTo(squad, m3TurnAt + Squad.STRIKE_TURN + STEP);
            Assert.AreEqual(1, squad.StrikersAt(foe), "m3's turn, never given back, ran out after four seconds");

            foe.gameObject.SetActive(false);
            squad.Begin(STEP);
            squad.End();
            Assert.AreEqual(0, squad.StrikersAt(foe), "A foe gone takes every turn at it with it");

            foe.gameObject.SetActive(true);
            Assert.IsFalse(squad.MayStrike(rear.Context, foe), "The rear rank waits for a place in front");
            Assert.IsTrue(squad.MayStrike(rear.Context, foe, frontOnly: false), "…unless every rank strikes (archers)");
        }

        // ---------- the pace, turns, holes, the ranks ----------

        [Test]
        public void Squad_PaceFactor_FollowsCohesion()
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            var members = new List<GraphRig>();
            for (int i = 0; i < 8; i++)
                members.Add(Member(squad, new Vector3((i - 3.5f) * 1.8f, 0f, 0f)));
            squad.Begin(0f);
            squad.End();
            Assert.AreEqual(1, squad.Ranks, "Set-up: one rank of eight");
            var slots = members.Select(m => SlotPoint(squad, m)).ToList();

            float[] expected = { 0.2f, 0.6f, 1f };
            int[] inPlace = { 3, 5, 8 };
            for (int k = 0; k < inPlace.Length; k++)
            {
                for (int i = 0; i < members.Count; i++)
                    members[i].Actuator.Position = slots[i] + (i < inPlace[k] ? Vector3.zero : new Vector3(0f, 0f, -5f));
                squad.Begin(STEP);
                squad.End();

                Assert.AreEqual(inPlace[k] / 8f, squad.Cohesion, 1e-5f, $"{inPlace[k]} of 8 in their slots");
                Assert.AreEqual(expected[k], squad.PaceFactor, 1e-4f,
                    $"Cohesion {squad.Cohesion:0.###}: Lerp(0.2, 1) over 0.4…0.85");
            }
        }

        [Test]
        public void Squad_ATurnWiderThan120Degrees_IsAnAboutFace_TheRearRankBecomesTheFront()
        {
            Squad squad = Formed(20, out List<GraphRig> members);
            Assert.AreEqual(2f, squad.Depth, "Set-up: two ranks");
            Assert.That((squad.Centre - new Vector3(0f, 0f, -1f)).magnitude, Is.LessThan(1e-4f), "Set-up: its middle");
            List<GraphRig> rear = members.Where(m => m.Context.Position.z < -1f).ToList();

            squad.Turn(new Vector3(0f, 0f, -1f), 60f, 0.02f);
            squad.End();

            Assert.That(Vector3.Angle(squad.Facing, Vector3.back), Is.LessThan(1e-3f), "Faces back at once, not 60°/s");
            Assert.That((squad.Anchor - new Vector3(0f, 0f, -2f)).magnitude, Is.LessThan(1e-4f),
                "The front is where the rear rank stood");
            Assert.That(rear.Select(m => squad.SlotIndexOf(m.Context)).OrderBy(slot => slot),
                Is.EqualTo(Enumerable.Range(0, 10)), "The rear rank holds the front slots now");
            Assert.IsTrue(rear.All(m => Front(squad, m)), "…it is the front rank");
        }

        [Test]
        public void Squad_TwoOfTheFrontFall_TheyAreReplacedFromBehind_ANewcomerTakesTheBack()
        {
            Squad squad = Formed(20, out List<GraphRig> members);
            squad.Begin(Squad.FORMING + STEP);                               // formed: holes are filled, not dealt anew
            squad.End();
            GraphRig third = members.First(m => squad.SlotIndexOf(m.Context) == 3);
            GraphRig fourth = members.First(m => squad.SlotIndexOf(m.Context) == 4);
            GraphRig behindThird = members.First(m => squad.SlotIndexOf(m.Context) == 13);
            GraphRig behindFourth = members.First(m => squad.SlotIndexOf(m.Context) == 14);

            squad.Leave(third.Context);
            squad.Leave(fourth.Context);
            squad.Begin(STEP);
            squad.End();

            Assert.AreEqual(3, squad.SlotIndexOf(behindThird.Context), "The one behind each stepped up");
            Assert.AreEqual(4, squad.SlotIndexOf(behindFourth.Context));
            List<int> held = members.Where(m => squad.Has(m.Context)).Select(m => squad.SlotIndexOf(m.Context)).ToList();
            Assert.That(held.OrderBy(slot => slot), Is.EqualTo(Enumerable.Range(0, 18)), "No hole left: slots 0…17");

            GraphRig newcomer = Member(squad, new Vector3(0f, 0f, -6f));
            squad.Begin(STEP);
            squad.End();
            Assert.AreEqual(18, squad.SlotIndexOf(newcomer.Context), "A newcomer takes the place at the back");
        }

        [Test]
        public void Squad_FilesChanged_TheRanksAreDealtAnew()
        {
            Squad squad = Formed(20, out List<GraphRig> members);

            squad.Configure(5, 1.8f, 2f, 3);
            squad.End();

            Assert.AreEqual(4, squad.Ranks);
            Assert.AreEqual(6f, squad.Depth, 1e-5f);
            GraphRig last = members.First(m => squad.SlotIndexOf(m.Context) == 19);
            Vector3 expected = squad.Anchor + squad.Right * 3.6f + squad.Facing * -6f;
            Assert.That((SlotPoint(squad, last) - expected).magnitude, Is.LessThan(1e-4f),
                "Slot 19: the last of four ranks of five — 3.6 m right, 6 m back");
            Assert.That(members.Select(m => squad.SlotIndexOf(m.Context)).OrderBy(slot => slot),
                Is.EqualTo(Enumerable.Range(0, 20)), "Every one has a slot of the new ranks");
        }

        // ---------- March To Target ----------

        [Test]
        public void MarchToTarget_TheFrontStopsStopAtShortOfTheTarget_NoFasterThanSpeedTimesPace()
        {
            Squad squad = Formed(20, out List<GraphRig> members);
            var command = new AgentContext(null, new Blackboard()) { Leads = squad };
            Transform target = Thing("Target", new Vector3(0f, 0f, 20f));
            command.BrainTarget = target;
            var march = new MarchToTargetTask { Speed = 2.2f, StopAt = 2.5f };
            const float DT = 0.02f;

            float fastest = 0f;
            Vector3 last = squad.Anchor;
            int still = 0;
            for (int i = 0; i < 2000 && still < 10; i++)
            {
                squad.Begin(DT);
                float pace = squad.PaceFactor;
                Assert.AreEqual(TaskStatus.Running, march.Tick(command, DT), "It leads until taken away");
                squad.End();
                float moved = (squad.Anchor - last).magnitude;
                fastest = Mathf.Max(fastest, moved / (march.Speed * pace * DT));
                still = moved < 1e-6f ? still + 1 : 0;
                last = squad.Anchor;
                foreach (GraphRig member in members)
                    member.Actuator.Position = SlotPoint(squad, member);    // the members keep up with their slots
            }

            Assert.AreEqual(10, still, "Set-up: the anchor stopped");
            Assert.That(fastest, Is.LessThanOrEqualTo(1f + 1e-3f), "Never faster than Speed × the pace");
            Assert.That(squad.AheadOfFront(target.position), Is.EqualTo(march.StopAt).Within(march.Speed * DT),
                "The front stops Stop At short of the target");
            Assert.That(Vector3.Angle(squad.Facing, Flat(target.position - squad.Centre)), Is.LessThan(1f), "Facing it");

            // Off to one side: it wheels round to face it and stops short of it all the same
            target.position = new Vector3(8f, 0f, 30f);
            for (int i = 0; i < 2000; i++)
            {
                squad.Begin(DT);
                march.Tick(command, DT);
                squad.End();
                foreach (GraphRig member in members)
                    member.Actuator.Position = SlotPoint(squad, member);
            }

            Assert.That(Vector3.Angle(squad.Facing, Flat(target.position - squad.Centre)), Is.LessThan(1f), "Wheeled to face it");
            Assert.That(squad.AheadOfFront(target.position), Is.EqualTo(march.StopAt).Within(march.Speed * DT));
        }

        // ---------- Keep Formation ----------

        [Test]
        public void KeepFormation_FrontOnlyOff_ARearRankMemberStrikesFromItsSlot()
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            squad.Configure(6, 1.8f, 2f, 12);
            var members = new List<GraphRig>();
            for (int rank = 0; rank < 2; rank++)
            {
                for (int file = 0; file < 6; file++)
                    members.Add(Member(squad, new Vector3((file - 2.5f) * 1.8f, 0f, -rank * 2f)));
            }

            squad.Begin(0f);
            squad.End();
            GraphRig archer = members.First(m => squad.SlotIndexOf(m.Context) == 8);
            Assert.IsFalse(Front(squad, archer), "Set-up: of the rear rank");
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            _created.Add(bow);
            archer.GiveWeapon(bow);
            Transform foe = Thing("Foe", new Vector3(0f, 0f, 10f));
            archer.Context.BrainTarget = foe;
            archer.Actuator.Position = SlotPoint(squad, archer);
            Assert.That(Flat(foe.position - archer.Context.Position).magnitude, Is.EqualTo(12f).Within(1.5f),
                "Set-up: about 12 m from the foe, within the bow's 14");

            Assert.AreEqual(TaskStatus.Success, new KeepFormationTask { FrontOnly = false }.Tick(archer.Context, 0.02f),
                "Every rank strikes: In Reach from its slot");

            squad.EndStrike(archer.Context);
            var frontOnly = new KeepFormationTask { FrontOnly = true };
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(TaskStatus.Running, frontOnly.Tick(archer.Context, 0.02f), "Front only: the rear rank waits");
        }

        [Test]
        public void KeepFormation_Settled_StaysUntilItsSlotIs0_6mAway()
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            GraphRig member = Member(squad, Vector3.zero);
            squad.Begin(0f);
            squad.End();
            Vector3 slot = SlotPoint(squad, member);
            var keep = new KeepFormationTask();
            const float DT = 0.02f;

            member.Actuator.Position = slot + new Vector3(0.2f, 0f, 0f);
            int stops = member.Actuator.Stops;
            Assert.AreEqual(TaskStatus.Running, keep.Tick(member.Context, DT));
            Assert.AreEqual(stops + 1, member.Actuator.Stops, "0.2 m off a slot that stands: it stops (settled)");

            member.Actuator.Position = slot + new Vector3(0.5f, 0f, 0f);
            keep.Tick(member.Context, DT);
            Vector3 before = member.Actuator.Position;
            member.Actuator.Apply(DT);
            Assert.AreEqual(stops + 2, member.Actuator.Stops, "Pushed to 0.5 m: still settled, it stops");
            Assert.AreEqual(before, member.Actuator.Position, "…and does not step");

            member.Actuator.Position = slot + new Vector3(0.7f, 0f, 0f);
            keep.Tick(member.Context, DT);
            before = member.Actuator.Position;
            member.Actuator.Apply(DT);
            Vector3 stepped = member.Actuator.Position - before;
            Assert.That(Vector3.Angle(stepped, slot - before), Is.LessThan(1f), "0.7 m: it steps back to its slot");
            Assert.That(stepped.magnitude / DT, Is.EqualTo(0.7f * 2f).Within(1e-3f),
                "…making up the distance at Gain 2: 1.4 m/s");
        }

        // ---------- helpers ----------

        private readonly struct Seen
        {
            public readonly float Now;
            public readonly Transform Target;

            public Seen(float now, Transform target)
            {
                Now = now;
                Target = target;
            }
        }

        // Two members at the banner (the squad's middle): the first sees A from the start (until aSeenUntil), the second
        // sees B from 1 s on; the focus after every step of an eighth of a second, until the time is out
        private List<Seen> Watch(Squad squad, GraphRig first, Transform a, GraphRig second, Transform b, float aSeenUntil,
            float until)
        {
            var focus = new List<Seen>();
            squad.Saw(first.Context, a);
            squad.Begin(0f);
            focus.Add(new Seen(squad.Now, squad.Target));
            while (squad.Now < until)
            {
                if (squad.Now < aSeenUntil)
                    squad.Saw(first.Context, a);
                if (squad.Now >= 1f)
                    squad.Saw(second.Context, b);
                squad.Begin(STEP);
                squad.End();
                focus.Add(new Seen(squad.Now, squad.Target));
            }

            return focus;
        }

        private Squad TwoWatchers(out GraphRig first, out GraphRig second)
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            first = Member(squad, new Vector3(-0.9f, 0f, 0f));
            second = Member(squad, new Vector3(0.9f, 0f, 0f));
            return squad;
        }

        // Members standing in two ranks of ten at the banner (the origin, facing +z), dealt
        private Squad Formed(int count, out List<GraphRig> members)
        {
            var squad = new Squad(Thing("Banner", Vector3.zero));
            squad.Configure(10, 1.8f, 2f, 3);
            members = new List<GraphRig>();
            for (int i = 0; i < count; i++)
            {
                int rank = i / 10;
                int file = i % 10;
                members.Add(Member(squad, new Vector3((file - 4.5f) * 1.8f, 0f, -rank * 2f)));
            }

            squad.Begin(0f);
            squad.End();
            return squad;
        }

        private static GraphRig Member(Squad squad, Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            squad.Join(rig.Context);
            return rig;
        }

        private static bool Front(Squad squad, GraphRig member)
        {
            squad.SlotOf(member.Context, out _, out _, out _, out bool front);
            return front;
        }

        private static Vector3 SlotPoint(Squad squad, GraphRig member)
        {
            Assert.IsTrue(squad.SlotOf(member.Context, out Vector3 point, out _, out _, out _), "Set-up: it has a slot");
            return point;
        }

        // Steps of an eighth of a second (the squad's step: begin, end) until its clock reads at least this
        private static void StepTo(Squad squad, float now)
        {
            while (squad.Now < now - 1e-4f)
            {
                squad.Begin(STEP);
                squad.End();
            }
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
