using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Dash on a real agent (handbook §20b.4 p. 6, §19.2, §20.3): the body shows the weapon's dash of that direction
    /// and goes exactly its distance over its time, relative to the target; Distance × scales it; a dash that has
    /// started is not cut short; no dash that way, a blocked way, no target — Blocked; Aside tries the other side.
    /// </summary>
    public sealed class DashTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;
        private const float DISTANCE = 2f;
        private const float TIME = 0.5f;

        private GraphRig _rig;
        private CharacterActionsWatch _actions;
        private Transform _target;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _actions = new CharacterActionsWatch(_rig.Context);       // the presentation, as it sees the character's actions
            _target = _rig.CreateTargetAt(new Vector3(5f, 0f, 0f));    // the body stands at the origin, the target east
            _rig.Agent.Blackboard.Set(TARGET, _target);
            WeaponAsset weapon = WeaponAsset.Create<WeaponAsset>(new WeaponMove());
            weapon.Dashes.Add(new WeaponDash { Direction = DashDirection.Back, Action = "Dash Back", Distance = DISTANCE, Time = TIME });
            weapon.Dashes.Add(new WeaponDash { Direction = DashDirection.Left, Action = "Dash Left", Distance = DISTANCE, Time = TIME });
            weapon.Dashes.Add(new WeaponDash { Direction = DashDirection.Right, Action = "Dash Right", Distance = DISTANCE, Time = TIME });
            _rig.GiveWeapon(weapon);
        }

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        [Test]
        public void Dash_Back_ShowsItsAction_GoesTheClipsDistanceAwayFromTheTarget_OverTheClipsTime()
        {
            var dash = new DashTask { Target = TARGET, Direction = DashDirection.Back };

            float lasted = Run(dash, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            CollectionAssert.AreEqual(new[] { "Dash Back" }, _actions.Played);
            Assert.AreEqual(TIME, lasted, DT + 1e-4f, "As long as the clip");
            Assert.AreEqual(-DISTANCE, _rig.Actuator.Position.x, 1e-3f, "As far as the clip, straight away from the target");
            Assert.AreEqual(0f, _rig.Actuator.Position.z, 1e-3f);
        }

        [Test]
        public void Dash_DistanceScale_ScalesHowFarItGoes()
        {
            Run(new DashTask { Target = TARGET, Direction = DashDirection.Back, DistanceScale = 1.5f }, out _);

            Assert.AreEqual(-DISTANCE * 1.5f, _rig.Actuator.Position.x, 1e-3f);
        }

        [Test]
        public void Dash_AStartedDash_IsNotCutShort()
        {
            var dash = new DashTask { Target = TARGET, Direction = DashDirection.Back };

            Step(dash);

            Assert.IsTrue(dash.CommitsFor(_rig.Context), "The Brain does not take the character elsewhere mid-dash");
        }

        [Test]
        public void Dash_NoDashThatWayOnTheWeapon_Blocked_NothingShown()
        {
            Assert.AreEqual(TaskStatus.Failure, Step(new DashTask { Target = TARGET, Direction = DashDirection.Forward }));
            Assert.IsEmpty(_actions.Played);
            Assert.AreEqual(Vector3.zero, _rig.Actuator.Position);
        }

        [Test]
        public void Dash_SomethingInTheWay_Blocked()
        {
            _rig.Actuator.DashBlocked = _ => true;

            Assert.AreEqual(TaskStatus.Failure, Step(new DashTask { Target = TARGET, Direction = DashDirection.Back }));
            Assert.AreEqual(0, _rig.Actuator.Dashes);
        }

        [Test]
        public void Dash_NoTarget_Blocked()
        {
            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);

            Assert.AreEqual(TaskStatus.Failure, Step(new DashTask { Target = TARGET }));
        }

        [Test]
        public void Dash_Aside_OneSideBlocked_GoesTheOtherSide()
        {
            _rig.Actuator.DashBlocked = displacement => displacement.z > 0f;    // the target east: left is north

            for (int i = 0; i < 6; i++)
            {
                _rig.Actuator.Position = Vector3.zero;
                Run(new DashTask { Target = TARGET, Direction = DashDirection.Aside }, out TaskStatus status);

                Assert.AreEqual(TaskStatus.Success, status);
                Assert.AreEqual(-DISTANCE, _rig.Actuator.Position.z, 1e-3f, "Always right: left is blocked");
            }
        }

        // ---------- the rig ----------

        private TaskStatus Step(AgentTask task)
        {
            _rig.Context.Advance(DT);
            TaskStatus status = task.Tick(_rig.Context, DT);
            _rig.Actuator.Apply(DT);
            return status;
        }

        private float Run(AgentTask task, out TaskStatus status)
        {
            float time = 0f;
            status = TaskStatus.Running;
            for (int i = 0; i < 200 && status == TaskStatus.Running; i++)
            {
                status = Step(task);
                time += DT;
            }

            return time;
        }
    }
}
