using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The ring of places around a target (handbook §18.3): the attackers cut the circle evenly between them — two
    /// stand opposite, three at 120°, ten at 36° — measured from where the first one stands, and each of the others
    /// takes the place nearest to where it already is. So ten attackers surround the target instead of crowding the
    /// side they came from, and nobody walks round it for nothing.
    /// </summary>
    public sealed class AttackRingTests
    {
        private const float DT = 0.1f;

        private static Vector3 At(float degrees, float radius = 5f) =>
            Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.forward * radius;

        private static float Angle(AttackRing ring, object agent, float standingAt) =>
            ring.AngleFor(agent, At(standingAt));

        // The first attacker: the one that already fights, the places are measured from it
        private static float Lead(AttackRing ring, object agent, float standingAt) =>
            ring.AngleFor(agent, At(standingAt), anchor: true);

        private static float Apart(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b));

        // Where an agent stands once they have all come: a place moves while the ring is filling up (one more agent —
        // one more place for everybody), so what each of them holds is read at the end, not as it joins
        private static float Place(AttackRing ring, object agent) => ring.PlaceOf(agent).Value;

        [Test]
        public void Ring_OneAgent_KeepsTheSideItIsAlreadyOn()
        {
            var ring = new AttackRing();

            Assert.AreEqual(200f, Angle(ring, "alone", 200f), 1e-3f);
            Assert.AreEqual(1, ring.Count);
        }

        [Test]
        public void Ring_TwoAgents_StandOppositeEachOther_TheFirstWhereItIs()
        {
            var ring = new AttackRing();
            float first = Angle(ring, "first", 270f);
            float second = Angle(ring, "second", 276f);                 // it comes up on the first one's side

            Assert.AreEqual(270f, first, 1e-3f, "The first one keeps its side");
            Assert.AreEqual(180f, Apart(first, second), 1e-3f, "The other goes to the far side: the target is between them");
        }

        [Test]
        public void Ring_ThreeAgents_ShareTheCircleInThirds()
        {
            var ring = new AttackRing();
            Angle(ring, "a", 0f);
            Angle(ring, "b", 100f);
            Angle(ring, "c", 250f);

            float a = Place(ring, "a");
            float b = Place(ring, "b");
            float c = Place(ring, "c");
            Assert.AreEqual(120f, Apart(a, b), 1e-3f);
            Assert.AreEqual(120f, Apart(b, c), 1e-3f);
            Assert.AreEqual(120f, Apart(a, c), 1e-3f);
        }

        [Test]
        public void Ring_TenAgentsFromOneSide_AreSpreadEvenlyRoundTheTarget()
        {
            var ring = new AttackRing();
            for (int i = 0; i < 10; i++)
                Angle(ring, "agent " + i, 10f + i);

            var angles = Enumerable.Range(0, 10).Select(i => Place(ring, "agent " + i)).ToList();
            CollectionAssert.AllItemsAreUnique(angles.Select(a => Mathf.RoundToInt(a)).ToList());
            Assert.AreEqual(36f, ring.Gap, 1e-3f, "Ten of them cut the circle into ten");

            var sorted = angles.OrderBy(a => a).ToList();
            for (int i = 1; i < sorted.Count; i++)
                Assert.AreEqual(36f, sorted[i] - sorted[i - 1], 1e-2f, "Evenly, all the way round");
        }

        [Test]
        public void Ring_OneMoreJoins_NobodyIsSentAcrossTheCircle_ThePlacesShiftByOneGapAtMost()
        {
            var ring = new AttackRing();
            Angle(ring, "first", 0f);                                   // round the target: 0, 70, 140, 210, 280
            Angle(ring, "b", 70f);
            Angle(ring, "c", 140f);
            Angle(ring, "d", 210f);
            Angle(ring, "e", 280f);
            var before = new[] { "b", "c", "d", "e" }.Select(a => Place(ring, a)).ToList();

            Angle(ring, "latecomer", 300f);                             // one more comes up at the back

            var after = new[] { "b", "c", "d", "e" }.Select(a => Place(ring, a)).ToList();
            for (int i = 0; i < before.Count; i++)
                Assert.That(Apart(before[i], after[i]), Is.LessThan(ring.Gap + 1e-3f), "Nobody walks round the target anew");
        }

        [Test]
        public void Ring_EachOfThemTakesThePlaceNearestToWhereItStands()
        {
            var ring = new AttackRing();
            Angle(ring, "north", 0f);                                   // the anchor: places at 0, 90, 180, 270
            Angle(ring, "east", 95f);
            Angle(ring, "south", 186f);
            Angle(ring, "west", 274f);

            Assert.That(Apart(Place(ring, "east"), 95f), Is.LessThan(45f),
                "It does not walk past another place to get to its own");
            Assert.That(Apart(Place(ring, "south"), 186f), Is.LessThan(45f));
            Assert.That(Apart(Place(ring, "west"), 274f), Is.LessThan(45f));
        }

        [Test]
        public void Ring_TheFirstAttackerMoves_TwoThatSteppedPastEachOther_DoNotSwapPlaces()
        {
            // A crowd jostles: two waiting side by side step past each other. The fighter shuffling as it swings must not
            // deal the places anew in the order they stand now — each would get the other's place, and both turn round
            var ring = new AttackRing();
            Lead(ring, "first", 0f);
            Angle(ring, "b", 110f);
            Angle(ring, "c", 130f);
            float b = Place(ring, "b");
            float c = Place(ring, "c");

            Angle(ring, "b", 140f);                                     // b has stepped past c
            Angle(ring, "c", 125f);
            Lead(ring, "first", 30f);                                  // and the fighter moves on

            Assert.AreEqual(30f, Apart(b, Place(ring, "b")), 1e-3f, "b's place turned with the fighter, no more");
            Assert.AreEqual(30f, Apart(c, Place(ring, "c")), 1e-3f, "c's place turned with the fighter, no more");
        }

        [Test]
        public void Ring_TheFirstAttackerShufflesALittle_ThePlacesStayPut()
        {
            var ring = new AttackRing();
            Lead(ring, "first", 0f);
            float mine = Angle(ring, "mine", 170f);

            for (int i = 0; i < 10; i++)                                // a fighter's footwork as it swings
                Lead(ring, "first", (i % 2 == 0 ? 1f : -1f) * (AttackRing.ANCHOR_SLACK - 1f));

            Assert.AreEqual(mine, Place(ring, "mine"), 1e-3f, "Places do not shiver with the fighter's feet");

            Lead(ring, "first", 60f);                                  // it has gone round the target
            Assert.AreEqual(60f, Apart(mine, Place(ring, "mine")), 1e-3f, "Then they follow it");
        }

        [Test]
        public void Ring_AnAgentKeepsItsPlace_WhileNobodyJoinsOrLeaves()
        {
            var ring = new AttackRing();
            Angle(ring, "first", 0f);
            float mine = Angle(ring, "mine", 170f);

            ring.Step(DT);
            float later = Angle(ring, "mine", 120f);                    // it has walked a long way round
            ring.Step(DT);
            float latest = Angle(ring, "mine", 100f);

            Assert.AreEqual(mine, later, 1e-3f, "Its place does not move under it");
            Assert.AreEqual(mine, latest, 1e-3f);
        }

        [Test]
        public void Ring_AnAgentThatStoppedComing_LetsItsPlaceGo_AndTheRestShareTheCircleAnew()
        {
            var ring = new AttackRing();
            Angle(ring, "first", 0f);
            Angle(ring, "second", 120f);
            Angle(ring, "third", 240f);
            Assert.AreEqual(120f, ring.Gap, 1e-3f);

            // the third goes on to something else and stops asking; the other two keep coming
            for (float waited = 0f; waited <= AttackRing.FORGET + 2 * DT; waited += DT)
            {
                ring.Step(DT);
                Angle(ring, "first", 0f);
                Angle(ring, "second", 120f);
            }

            Assert.AreEqual(2, ring.Count, "The one that stopped coming is gone from the ring");
            Assert.AreEqual(180f, ring.Gap, 1e-3f, "The two that are left stand opposite each other");
            Assert.AreEqual(180f, Apart(Angle(ring, "first", 0f), Angle(ring, "second", 120f)), 1e-3f);
        }

        [Test]
        public void Ring_AnAgentThatJoinsLate_IsNotForgotten_TheClockIsTheFightsOwn()
        {
            var ring = new AttackRing();
            for (float waited = 0f; waited < 10f; waited += DT)         // a long fight: two of them have been at it
            {
                ring.Step(DT);
                Angle(ring, "first", 0f);
                Angle(ring, "second", 180f);
            }

            float latecomer = Angle(ring, "latecomer", 90f);            // one more comes up, its own clock at zero
            ring.Step(DT);
            Angle(ring, "first", 0f);
            Angle(ring, "second", 180f);

            Assert.AreEqual(3, ring.Count, "The one that came last keeps its place");
            Assert.AreEqual(latecomer, ring.PlaceOf("latecomer").Value, 1e-3f);
            Assert.AreEqual(120f, ring.Gap, 1e-3f, "The three of them share the circle in thirds");
        }

        [Test]
        public void Ring_ThePlacesAreMeasuredFromTheFirstAttacker_NotFromTheWorld()
        {
            var one = new AttackRing();
            var two = new AttackRing();

            Angle(one, "leader", 40f);
            Angle(two, "leader", 200f);

            Assert.AreEqual(220f, Angle(one, "other", 300f), 1e-3f, "Opposite its own leader");
            Assert.AreEqual(20f, Angle(two, "other", 300f), 1e-3f, "...and opposite its own, wherever that stands");
        }

        [Test]
        public void Ring_Release_GivesThePlaceUpAtOnce()
        {
            var ring = new AttackRing();
            Angle(ring, "first", 0f);
            Angle(ring, "second", 180f);

            ring.Release("second");

            Assert.AreEqual(1, ring.Count);
            Assert.IsNull(ring.PlaceOf("second"));
            Assert.AreEqual(0f, ring.PlaceOf("first").Value, 1e-3f);
        }
    }
}
