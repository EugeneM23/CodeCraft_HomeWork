using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SmokeScreenTests
    {
        private float _now;

        private SmokeScreen Create(int capacity = 4) => new(capacity, () => _now);

        [SetUp]
        public void SetUp() => _now = 100f;

        [Test]
        public void Empty_IsInactive_AndHidesNothing()
        {
            SmokeScreen screen = Create();

            Assert.That(screen.IsActive, Is.False);
            Assert.That(screen.Contains(Vector3.zero), Is.False);
            Assert.That(screen.BlocksLine(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f)), Is.False);
        }

        [Test]
        public void Contains_PointsWithinTheRadiusOnTheGround_WhateverTheirHeight()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            Assert.That(screen.IsActive, Is.True);
            Assert.That(screen.Contains(new Vector3(5f, 0f, 0f)), Is.True);
            Assert.That(screen.Contains(new Vector3(0f, 30f, 6.9f)), Is.True);
            Assert.That(screen.Contains(new Vector3(5f, 0f, 5f)), Is.False);
            Assert.That(screen.Contains(new Vector3(8f, 0f, 0f)), Is.False);
        }

        [Test]
        public void AScreen_IsGoneWhenItsTimeIsUp()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            _now += 11.9f;
            Assert.That(screen.Contains(Vector3.zero), Is.True);

            _now += 0.2f;
            Assert.That(screen.Contains(Vector3.zero), Is.False);
            Assert.That(screen.IsActive, Is.False);
            Assert.That(screen.BlocksLine(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f)), Is.False);
        }

        [Test]
        public void BlocksLine_ALineThroughTheSmoke()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            Assert.That(screen.BlocksLine(new Vector3(-20f, 0f, 0f), new Vector3(20f, 5f, 0f)), Is.True);
            Assert.That(screen.BlocksLine(new Vector3(-20f, 0f, 6f), new Vector3(20f, 0f, 6f)), Is.True);
        }

        [Test]
        public void BlocksLine_NotALinePastTheSmoke()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            Assert.That(screen.BlocksLine(new Vector3(-20f, 0f, 8f), new Vector3(20f, 0f, 8f)), Is.False);
            // On a line through the smoke, but stopping short of it.
            Assert.That(screen.BlocksLine(new Vector3(-20f, 0f, 0f), new Vector3(-10f, 0f, 0f)), Is.False);
        }

        [Test]
        public void BlocksLine_WithAnEndInsideTheSmoke()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            Assert.That(screen.BlocksLine(new Vector3(-20f, 0f, 0f), new Vector3(-3f, 0f, 0f)), Is.True);
            Assert.That(screen.BlocksLine(Vector3.zero, Vector3.zero), Is.True);
        }

        [Test]
        public void AnyOfSeveralScreens_Hides()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);
            screen.Add(new Vector3(50f, 0f, 0f), 7f, 12f);

            Assert.That(screen.Contains(new Vector3(52f, 0f, 0f)), Is.True);
            Assert.That(screen.Contains(new Vector3(25f, 0f, 0f)), Is.False);
        }

        [Test]
        public void PastItsCapacity_TheScreenEndingSoonestGivesWay()
        {
            SmokeScreen screen = Create(2);
            screen.Add(new Vector3(0f, 0f, 0f), 7f, 5f);
            screen.Add(new Vector3(50f, 0f, 0f), 7f, 20f);
            screen.Add(new Vector3(100f, 0f, 0f), 7f, 20f);

            Assert.That(screen.Contains(new Vector3(0f, 0f, 0f)), Is.False);
            Assert.That(screen.Contains(new Vector3(50f, 0f, 0f)), Is.True);
            Assert.That(screen.Contains(new Vector3(100f, 0f, 0f)), Is.True);
        }

        [Test]
        public void Clear_TakesEveryScreenAway()
        {
            SmokeScreen screen = Create();
            screen.Add(Vector3.zero, 7f, 12f);

            screen.Clear();

            Assert.That(screen.IsActive, Is.False);
            Assert.That(screen.Contains(Vector3.zero), Is.False);
        }
    }
}
