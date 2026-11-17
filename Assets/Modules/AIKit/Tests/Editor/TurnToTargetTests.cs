using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Turn To Target on a real agent: the body turns to the target in place over Turn Time while the animator shows a
    /// step turn to that side (a big one for a target behind); a target almost in front is faced with no turn shown; a
    /// turn that has started is not cut short.
    /// </summary>
    public sealed class TurnToTargetTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private CharacterActionsWatch _actions;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _actions = new CharacterActionsWatch(_rig.Context);       // the presentation, as it sees the character's actions
            _target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _rig.Context.Blackboard.Set(TARGET, _target.transform);
            if (_rig.Context.Self != null)
                _rig.Context.Self.rotation = Quaternion.LookRotation(_rig.Actuator.Facing);    // where the body looks
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            GraphRig.DestroyAll();
        }

        // The body faces +X at the start (the test body)
        private float AngleToTarget()
        {
            Vector3 to = _target.transform.position - _rig.Actuator.Position;
            to.y = 0f;
            return Vector3.Angle(_rig.Actuator.Facing, to);
        }

        private TaskStatus Step(AgentTask task)
        {
            _rig.Context.Advance(DT);
            TaskStatus status = task.Tick(_rig.Context, DT);
            _rig.Actuator.Apply(DT);
            return status;
        }

        [Test]
        public void Turn_ATargetAtTheSide_ShowsTheTurnToThatSide_TurnsOverTurnTime_ThenDone()
        {
            _target.transform.position = new Vector3(0f, 0f, 5f);              // 90° to the left
            var turn = new TurnToTargetTask { Target = TARGET, TurnTime = 1f };

            Assert.AreEqual(TaskStatus.Running, Step(turn));
            CollectionAssert.AreEqual(new[] { turn.Left }, _actions.Played, "The step turn to the left is shown");
            Assert.IsTrue(turn.CommitsFor(_rig.Context), "A turn that has started is not cut short");

            for (int i = 1; i < Mathf.RoundToInt(0.5f / DT); i++)
                Step(turn);
            Assert.That(AngleToTarget(), Is.InRange(35f, 55f), "Half the time: about half the way round");

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 40 && status == TaskStatus.Running; i++)
                status = Step(turn);

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.Less(AngleToTarget(), 2f, "Facing the target");
        }

        [Test]
        public void Turn_ATargetBehind_ShowsTheBigTurn()
        {
            _target.transform.position = new Vector3(-5f, 0f, -1f);
            var turn = new TurnToTargetTask { Target = TARGET };

            Step(turn);

            CollectionAssert.AreEqual(new[] { turn.Right180 }, _actions.Played, "Behind and to the right: the big turn");
        }

        [Test]
        public void Turn_ATargetAlmostInFront_NoTurnShown_FacesIt_Done()
        {
            _target.transform.position = new Vector3(5f, 0f, 0.8f);
            var turn = new TurnToTargetTask { Target = TARGET };

            Assert.AreEqual(TaskStatus.Success, Step(turn));
            CollectionAssert.IsEmpty(_actions.Played, "Below Min Angle: no step turn");
            Assert.Less(AngleToTarget(), 1f);
        }

        [Test]
        public void Turn_NoTarget_Fails()
        {
            _rig.Context.Blackboard.Set(TARGET, (Transform)null);

            Assert.AreEqual(TaskStatus.Failure, Step(new TurnToTargetTask { Target = TARGET }));
        }

        [Test]
        public void Turn_TakenAwayMidway_TheTurnShownIsDropped()
        {
            _target.transform.position = new Vector3(0f, 0f, -5f);
            var turn = new TurnToTargetTask { Target = TARGET };
            Step(turn);

            turn.Interrupt(_rig.Context);

            Assert.AreEqual(1, _actions.Drops, "The step turn does not keep playing after the node");
        }

        [Test]
        public void Turn_ABodyTurnedByTheClip_TheClipIsAimedAtTheTarget_TheNodeOnlyFacesItAtTheEnd()
        {
            _rig.Actuator.ActionsByClip = true;
            _target.transform.position = new Vector3(0f, 0f, 5f);              // 90° to the left
            var turn = new TurnToTargetTask { Target = TARGET, TurnTime = 1f };
            Vector3 facing = _rig.Actuator.Facing;

            Step(turn);

            Assert.AreSame(_target.transform, _rig.Actuator.ClipTurnTarget, "The clip's turn is aimed at the target");
            Assert.AreEqual(TurnToTargetTask.TURN, _rig.Actuator.ClipTurnAngle, "A quarter turn clip");
            for (int i = 1; i < Mathf.RoundToInt(0.5f / DT); i++)
                Step(turn);
            Assert.AreEqual(facing, _rig.Actuator.Facing, "Midway the node does not turn the body: the clip does");

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 40 && status == TaskStatus.Running; i++)
                status = Step(turn);

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.Less(AngleToTarget(), 2f, "At the end it faces the target, whatever the clip left over");
            Assert.AreSame(_target.transform, _rig.Actuator.ClipTurnTarget,
                "Done, the clip still plays out aimed: the body lets the aim go once the action is over");
        }

        [Test]
        public void Turn_ABodyTurnedByTheClip_ATargetBehind_TheBigClipIsAimed_TakenAway_TheAimIsDropped()
        {
            _rig.Actuator.ActionsByClip = true;
            _target.transform.position = new Vector3(-5f, 0f, -1f);
            var turn = new TurnToTargetTask { Target = TARGET };

            Step(turn);
            Assert.AreEqual(TurnToTargetTask.BIG_TURN, _rig.Actuator.ClipTurnAngle, "A half turn clip");

            turn.Interrupt(_rig.Context);

            Assert.IsNull(_rig.Actuator.ClipTurnTarget, "Taken away: the clip is no longer aimed");
        }
    }
}
