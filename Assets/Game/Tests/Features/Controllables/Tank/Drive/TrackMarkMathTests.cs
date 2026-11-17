using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TrackMarkMathTests
    {
        private const float EXTEND = 0.2f;
        private static readonly Vector3 Forward = Vector3.forward;

        // Five wheels front to rear, 1 m apart.
        private static readonly Vector3[] Contacts =
        {
            new(0f, 0f, 2f), new(0f, 0f, 1f), new(0f, 0f, 0f), new(0f, 0f, -1f), new(0f, 0f, -2f)
        };

        private static float Points(bool[] grounded, List<Vector3> points) =>
            TrackMarkMath.Points(Contacts, grounded, Forward, EXTEND, points);

        [Test]
        public void AllGrounded_RunsFromAheadOfTheFirstToBehindTheLast()
        {
            List<Vector3> points = new();
            float length = Points(new[] { true, true, true, true, true }, points);

            Assert.That(points.Count, Is.EqualTo(7));
            Assert.That(points[0].z, Is.EqualTo(2f + EXTEND).Within(1e-5f));
            Assert.That(points[1].z, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(points[6].z, Is.EqualTo(-2f - EXTEND).Within(1e-5f));
            Assert.That(length, Is.EqualTo(4f + 2f * EXTEND).Within(1e-5f));
        }

        [Test]
        public void WheelInTheAir_IsSkipped()
        {
            List<Vector3> points = new();
            Points(new[] { true, true, false, true, true }, points);

            Assert.That(points.Count, Is.EqualTo(6));
            Assert.That(points[2].z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(points[3].z, Is.EqualTo(-1f).Within(1e-5f));
        }

        [Test]
        public void NoneGrounded_NoPointsNoLength()
        {
            List<Vector3> points = new() { Vector3.one };
            float length = Points(new[] { false, false, false, false, false }, points);

            Assert.That(points, Is.Empty);
            Assert.That(length, Is.EqualTo(0f));
        }

        [Test]
        public void OneGrounded_StretchesBothWaysAlongForward()
        {
            List<Vector3> points = new();
            float length = Points(new[] { false, false, true, false, false }, points);

            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[0].z, Is.EqualTo(EXTEND).Within(1e-5f));
            Assert.That(points[2].z, Is.EqualTo(-EXTEND).Within(1e-5f));
            Assert.That(length, Is.EqualTo(2f * EXTEND).Within(1e-5f));
        }

        [Test]
        public void Lead_Forward_IsTheFrontEnd()
        {
            List<Vector3> points = new();
            Points(new[] { true, true, true, true, true }, points);

            Assert.That(TrackMarkMath.Lead(points, 2f, out Vector3 lead), Is.EqualTo(1));
            Assert.That(lead.z, Is.EqualTo(2f + EXTEND).Within(1e-5f));
        }

        [Test]
        public void Lead_Reversing_IsTheRearEnd()
        {
            List<Vector3> points = new();
            Points(new[] { true, true, true, true, true }, points);

            Assert.That(TrackMarkMath.Lead(points, -2f, out Vector3 lead), Is.EqualTo(-1));
            Assert.That(lead.z, Is.EqualTo(-2f - EXTEND).Within(1e-5f));
        }

        [Test]
        public void Lead_StandingStillOrInTheAir_None()
        {
            List<Vector3> points = new();
            Points(new[] { true, true, true, true, true }, points);
            Assert.That(TrackMarkMath.Lead(points, 0f, out _), Is.EqualTo(0));

            Points(new[] { false, false, false, false, false }, points);
            Assert.That(TrackMarkMath.Lead(points, 2f, out _), Is.EqualTo(0));
        }

        private const float MAX_STEP = 0.5f;
        private static readonly Vector3 Start = new(0f, 0f, 2f);

        [Test]
        public void Sweep_ForwardStep_DigsFromTheFrontier()
        {
            int end = 1;
            Vector3 frontier = Start;

            bool dig = TrackMarkMath.Sweep(ref end, ref frontier, 1, Start + Vector3.forward * 0.1f, Forward, MAX_STEP,
                out Vector3 from);

            Assert.That(dig, Is.True);
            Assert.That(from.z, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(frontier.z, Is.EqualTo(2.1f).Within(1e-5f));
        }

        [Test]
        public void Sweep_LeadRockingBack_DigsNothingTwice()
        {
            int end = 1;
            Vector3 frontier = Start;

            // Rocks back 5 cm, then forward 10 cm past where it began: only the last 5 cm are new.
            bool back = TrackMarkMath.Sweep(ref end, ref frontier, 1, Start - Vector3.forward * 0.05f, Forward, MAX_STEP, out _);
            bool on = TrackMarkMath.Sweep(ref end, ref frontier, 1, Start + Vector3.forward * 0.05f, Forward, MAX_STEP,
                out Vector3 from);

            Assert.That(back, Is.False);
            Assert.That(on, Is.True);
            Assert.That(from.z, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void Sweep_FrontWheelLifting_KeepsTheFrontier()
        {
            int end = 1;
            Vector3 frontier = Start;

            // The front wheel lifts: the lead drops back a whole wheel, further than a step.
            bool lifted = TrackMarkMath.Sweep(ref end, ref frontier, 1, Start - Vector3.forward * 0.6f, Forward, MAX_STEP, out _);
            // It lands again, a little further on: only that little is new.
            bool landed = TrackMarkMath.Sweep(ref end, ref frontier, 1, Start + Vector3.forward * 0.1f, Forward, MAX_STEP,
                out Vector3 from);

            Assert.That(lifted, Is.False);
            Assert.That(landed, Is.True);
            Assert.That(from.z, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void Sweep_SidewaysStep_DigsOnlyTheGainAlongTheTrack()
        {
            int end = 1;
            Vector3 frontier = Start;
            Vector3 lead = Start + Vector3.right * 0.3f + Vector3.forward * 0.05f;

            bool dig = TrackMarkMath.Sweep(ref end, ref frontier, 1, lead, Forward, MAX_STEP, out Vector3 from);

            // A strip along the track, 5 cm long, ending at the lead: not one 30 cm across it.
            Assert.That(dig, Is.True);
            Assert.That(from.x, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(from.z, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void Sweep_ReversingLeadsWithTheRear()
        {
            int end = -1;
            Vector3 frontier = Start;

            bool dig = TrackMarkMath.Sweep(ref end, ref frontier, -1, Start - Vector3.forward * 0.1f, Forward, MAX_STEP, out _);

            Assert.That(dig, Is.True);
        }

        [Test]
        public void Sweep_ChangeOfEndOrJump_RestartsWithoutDigging()
        {
            int end = 1;
            Vector3 frontier = Start;
            Assert.That(TrackMarkMath.Sweep(ref end, ref frontier, -1, Start, Forward, MAX_STEP, out _), Is.False);
            Assert.That(end, Is.EqualTo(-1));

            end = 1;
            frontier = Start;
            Vector3 far = Start + Vector3.forward * 0.6f;
            Assert.That(TrackMarkMath.Sweep(ref end, ref frontier, 1, far, Forward, MAX_STEP, out _), Is.False);
            Assert.That(frontier.z, Is.EqualTo(far.z).Within(1e-5f));

            Assert.That(TrackMarkMath.Sweep(ref end, ref frontier, 0, far, Forward, MAX_STEP, out _), Is.False);
            Assert.That(end, Is.EqualTo(0));
        }
    }
}
