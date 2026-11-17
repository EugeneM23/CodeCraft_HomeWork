using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelTicksTests
    {
        private readonly List<string> _log = new();

        [SetUp]
        public void SetUp()
        {
            VoxelTicks.Clear();
            _log.Clear();
        }

        [TearDown]
        public void TearDown() => VoxelTicks.Clear();

        [Test]
        public void Listeners_TickByExecutionOrder_ThenByTheOrderTheyJoined()
        {
            VoxelTicks.Add(new Late(_log, "late"));
            VoxelTicks.Add(new Plain(_log, "plain1"));
            VoxelTicks.Add(new Early(_log, "early"));
            VoxelTicks.Add(new Plain(_log, "plain2"));

            VoxelTicks.Update(0.1f);

            CollectionAssert.AreEqual(new[] { "early", "plain1", "plain2", "late" }, _log);
        }

        [Test]
        public void EachPhase_TicksOnlyItsOwnListeners_WithTheGivenDelta()
        {
            var all = new AllPhases();
            VoxelTicks.Add(all);
            VoxelTicks.Add(new Plain(_log, "update only"));

            VoxelTicks.FixedUpdate(0.02f);
            VoxelTicks.LateUpdate(0.5f);

            Assert.AreEqual(0, _log.Count);
            Assert.AreEqual(0.02f, all.Fixed);
            Assert.AreEqual(0.5f, all.Late);
            Assert.AreEqual(0f, all.Update);
            Assert.AreEqual(2, VoxelTicks.UpdateCount);
            Assert.AreEqual(1, VoxelTicks.FixedUpdateCount);
            Assert.AreEqual(1, VoxelTicks.LateUpdateCount);
        }

        [Test]
        public void AddingTwice_TicksOnce_AndRemoveStopsTheTicks()
        {
            var plain = new Plain(_log, "a");
            VoxelTicks.Add(plain);
            VoxelTicks.Add(plain);

            VoxelTicks.Update(0.1f);
            VoxelTicks.Remove(plain);
            VoxelTicks.Update(0.1f);

            CollectionAssert.AreEqual(new[] { "a" }, _log);
            Assert.IsFalse(VoxelTicks.Contains(plain));
        }

        [Test]
        public void RemovedDuringAPass_StopsAtOnce_AddedDuringAPass_TicksFromTheNext()
        {
            var victim = new Plain(_log, "victim");
            var newcomer = new Plain(_log, "newcomer");
            VoxelTicks.Add(new Callback(_log, "killer", () =>
            {
                VoxelTicks.Remove(victim);
                VoxelTicks.Add(newcomer);
            }));
            VoxelTicks.Add(victim);

            VoxelTicks.Update(0.1f);
            CollectionAssert.AreEqual(new[] { "killer" }, _log);

            _log.Clear();
            VoxelTicks.Update(0.1f);
            CollectionAssert.AreEqual(new[] { "killer", "newcomer" }, _log);
        }

        [Test]
        public void AnException_IsLogged_AndTheRestTick()
        {
            VoxelTicks.Add(new Callback(_log, "thrower", () => throw new InvalidOperationException("boom")));
            VoxelTicks.Add(new Plain(_log, "after"));

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            VoxelTicks.Update(0.1f);

            CollectionAssert.AreEqual(new[] { "thrower", "after" }, _log);
        }

        [Test]
        public void Time_IsUnityTimeWithoutAnOwner_AndTheOwnersTimeWhenDriven()
        {
            VoxelTicks.SetTime(1234f);
            Assert.IsFalse(VoxelTicks.Driven);
            Assert.AreEqual(Time.time, VoxelTicks.Time);

            VoxelTicks.Driven = true;
            Assert.AreEqual(1234f, VoxelTicks.Time);

            VoxelTicks.SetTime(5f);
            Assert.AreEqual(5f, VoxelTicks.Time);
        }

        [Test]
        public void Clear_DropsEveryListener_AndTheOwner()
        {
            VoxelTicks.Add(new Plain(_log, "a"));
            VoxelTicks.Driven = true;

            VoxelTicks.Clear();
            VoxelTicks.Update(0.1f);

            Assert.AreEqual(0, _log.Count);
            Assert.IsFalse(VoxelTicks.Driven);
            Assert.AreEqual(0, VoxelTicks.UpdateCount);
        }

        private class Plain : IVoxelUpdate
        {
            private readonly List<string> _log;
            private readonly string _name;

            public Plain(List<string> log, string name)
            {
                _log = log;
                _name = name;
            }

            public virtual void VoxelUpdate(float deltaTime) => _log.Add(_name);
        }

        [DefaultExecutionOrder(-100)]
        private sealed class Early : Plain
        {
            public Early(List<string> log, string name) : base(log, name)
            {
            }
        }

        [DefaultExecutionOrder(10000)]
        private sealed class Late : Plain
        {
            public Late(List<string> log, string name) : base(log, name)
            {
            }
        }

        private sealed class Callback : Plain
        {
            private readonly System.Action _action;

            public Callback(List<string> log, string name, System.Action action) : base(log, name) => _action = action;

            public override void VoxelUpdate(float deltaTime)
            {
                base.VoxelUpdate(deltaTime);
                _action();
            }
        }

        private sealed class AllPhases : IVoxelUpdate, IVoxelLateUpdate, IVoxelFixedUpdate
        {
            public float Update;
            public float Late;
            public float Fixed;

            public void VoxelUpdate(float deltaTime) => Update = deltaTime;

            public void VoxelLateUpdate(float deltaTime) => Late = deltaTime;

            public void VoxelFixedUpdate(float deltaTime) => Fixed = deltaTime;
        }
    }
}
