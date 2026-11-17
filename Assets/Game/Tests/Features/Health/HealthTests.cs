using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HealthTests
    {
        [Test]
        public void Construction_ClampsTheStartingValueIntoRange()
        {
            Assert.That(new Health(50, 10).GetCurrent(), Is.EqualTo(10));
            Assert.That(new Health(-5, 10).GetCurrent(), Is.EqualTo(0));
        }

        [Test]
        public void Reduce_BelowZero_StopsAtZero_AndAnnouncesTheEmptyHealthOnce()
        {
            var health = new Health(5, 10);
            int empties = 0;
            health.OnHealthEmpty += () => empties++;

            bool reduced = health.Reduce(50);

            Assert.That(reduced, Is.True);
            Assert.That(health.GetCurrent(), Is.EqualTo(0));
            Assert.That(empties, Is.EqualTo(1));
        }

        [Test]
        public void Reduce_OnAnEmptyHealth_DoesNothingAndDoesNotDieAgain()
        {
            var health = new Health(0, 10);
            int empties = 0;
            int callbacks = 0;
            health.OnHealthEmpty += () => empties++;

            bool reduced = health.Reduce(3, () => callbacks++);

            Assert.That(reduced, Is.False);
            Assert.That(empties, Is.Zero);
            Assert.That(callbacks, Is.Zero);
        }

        [Test]
        public void Reduce_ByZero_ChangesNothing()
        {
            var health = new Health(10, 10);
            int changes = 0;
            health.OnHealthChanged += _ => changes++;

            Assert.That(health.Reduce(0), Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void Reduce_ByANegativeRange_Throws_AndKeepsTheHealth()
        {
            var health = new Health(10, 10);

            Assert.Throws<Exception>(() => health.Reduce(-1));
            Assert.That(health.GetCurrent(), Is.EqualTo(10));
        }

        [Test]
        public void Reduce_RunsTheCallbackAfterTheChange_AndBeforeTheDeath()
        {
            var health = new Health(4, 10);
            var order = new List<string>();
            health.OnHealthChanged += value => order.Add("changed:" + value);
            health.OnHealthEmpty += () => order.Add("empty");

            health.Reduce(4, () => order.Add("hit"));

            Assert.That(order, Is.EqualTo(new[] { "changed:0", "hit", "empty" }));
        }

        [Test]
        public void Add_StopsAtMax_AndRefusesWhenAlreadyFull()
        {
            var health = new Health(8, 10);
            int changes = 0;
            health.OnHealthChanged += _ => changes++;

            bool healed = health.Add(5);
            bool healedAgain = health.Add(5);

            Assert.That(healed, Is.True);
            Assert.That(healedAgain, Is.False);
            Assert.That(health.GetCurrent(), Is.EqualTo(10));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Add_ZeroOrNegative_IsRefused()
        {
            var health = new Health(5, 10);

            Assert.That(health.Add(0), Is.False);
            Assert.That(health.Add(-3), Is.False);
            Assert.That(health.GetCurrent(), Is.EqualTo(5));
        }

        [Test]
        public void SetCurrent_ToZero_Dies_AndSettingTheSameValueIsSilent()
        {
            var health = new Health(5, 10);
            int empties = 0;
            int changes = 0;
            health.OnHealthEmpty += () => empties++;
            health.OnHealthChanged += _ => changes++;

            health.SetCurrent(5);
            health.SetCurrent(0);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(empties, Is.EqualTo(1));
        }

        [Test]
        public void SetCurrent_AboveMax_IsClamped()
        {
            var health = new Health(5, 10);

            health.SetCurrent(99);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
            Assert.That(health.IsFull(), Is.True);
        }

        [Test]
        public void SetMax_BelowCurrent_PullsCurrentDown_AndReportsBoth()
        {
            var health = new Health(10, 10);
            int newMax = -1;
            int newCurrent = -1;
            health.OnMaxHealthChanged += value => newMax = value;
            health.OnHealthChanged += value => newCurrent = value;

            health.SetMax(4);

            Assert.That(newMax, Is.EqualTo(4));
            Assert.That(newCurrent, Is.EqualTo(4));
            Assert.That(health.GetPercent(), Is.EqualTo(1f));
        }

        [Test]
        public void SetMax_NeverDropsBelowOne()
        {
            var health = new Health(10, 10);

            health.SetMax(0);

            Assert.That(health.GetMax(), Is.EqualTo(1));
            Assert.That(health.GetCurrent(), Is.EqualTo(1));
            Assert.That(health.Exists(), Is.True);
        }

        [Test]
        public void SetMax_AboveCurrent_LeavesCurrent_AndHalvesThePercent()
        {
            var health = new Health(10, 10);
            int changes = 0;
            health.OnHealthChanged += _ => changes++;

            health.SetMax(20);

            Assert.That(health.GetCurrent(), Is.EqualTo(10));
            Assert.That(health.GetPercent(), Is.EqualTo(0.5f));
            Assert.That(changes, Is.Zero);
        }
    }
}
