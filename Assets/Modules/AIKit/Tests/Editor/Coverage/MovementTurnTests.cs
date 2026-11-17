using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Turn To Target (step 90 recheck): the turn is measured from where the body looks (its Forward — a Simple Body never
    /// turns its transform), a target that moves during the turn is faced where it is at the end, the turn lasts exactly
    /// Turn Time and holds the character until then, and a target behind to the left gets the big turn to the left.
    /// </summary>
    public sealed class MovementTurnTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private CharacterActionsWatch _actions;
        private GameObject _target;
        private GameObject _simple;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _actions = new CharacterActionsWatch(_rig.Context);
            _target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _rig.Context.Blackboard.Set(TARGET, _target.transform);
            _rig.Context.Self.rotation = Quaternion.LookRotation(_rig.Actuator.Facing);     // the body looks +X
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            if (_simple != null)
                Object.DestroyImmediate(_simple);
            GraphRig.DestroyAll();
        }

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
        public void TurnToTarget_ASimpleBodyAlreadyFacingTheTarget_ShowsNoTurn()
        {
            // A Simple Body looks where it last walked (Body.Forward) and never turns its transform (forward stays +Z)
            _simple = new GameObject("Simple") { hideFlags = HideFlags.HideAndDontSave };
            _simple.SetActive(false);
            var character = _simple.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = new SimpleBody { PlanarXY = false };
            character.Initialize();
            AgentContext context = character.Context;
            var watch = new CharacterActionsWatch(context);
            context.Actuator.Move(Vector3.right);
            context.Actuator.Apply(0.1f);
            Assert.That(Vector3.Angle(context.Actuator.Body.Forward, Vector3.right), Is.LessThan(0.01f), "Set-up: it looks +X");
            Assert.That(Vector3.Angle(_simple.transform.forward, Vector3.forward), Is.LessThan(0.01f), "…its transform still +Z");

            _target.transform.position = _simple.transform.position + new Vector3(5f, 0f, 0f);
            context.BrainTarget = _target.transform;
            var turn = new TurnToTargetTask { MinAngle = 30f };

            Assert.AreEqual(TaskStatus.Success, turn.Tick(context, DT), "The target is straight ahead: nothing to turn");
            CollectionAssert.IsEmpty(watch.Played, "No step turn is shown");
            Assert.That(Vector3.Angle(context.Actuator.Body.Forward, Vector3.right), Is.LessThan(1f), "It still looks +X");
        }

        [Test]
        public void TurnToTarget_TheTargetMovesDuringTheTurn_EndsFacingWhereItIsNow()
        {
            _target.transform.position = new Vector3(0f, 0f, 5f);
            var turn = new TurnToTargetTask { Target = TARGET, TurnTime = 1f };

            TaskStatus status = TaskStatus.Running;
            float time = 0f;
            bool moved = false;
            for (int i = 0; i < 40 && status == TaskStatus.Running; i++)
            {
                if (!moved && time >= 0.5f - 1e-4f)
                {
                    _target.transform.position = new Vector3(-5f, 0f, 1f);
                    moved = true;
                }

                status = Step(turn);
                time += DT;
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.That(time, Is.EqualTo(1f).Within(DT * 1.5f), "Done at Turn Time");
            Assert.Less(AngleToTarget(), 2f, "Facing the target where it is now");
        }

        [Test]
        public void TurnToTarget_TurnTime_DoneOnItsLastTick_CommitsUntilThen()
        {
            _target.transform.position = new Vector3(0f, 0f, 5f);           // 90° to the left
            var turn = new TurnToTargetTask { Target = TARGET, TurnTime = 0.3f };

            int doneAt = -1;
            for (int tick = 1; tick <= 10; tick++)
            {
                TaskStatus status = Step(turn);
                if (status == TaskStatus.Running)
                {
                    Assert.IsTrue(turn.CommitsFor(_rig.Context), $"Tick {tick}: a turn that has started holds the character");
                    continue;
                }

                Assert.AreEqual(TaskStatus.Success, status);
                doneAt = tick;
                break;
            }

            Assert.That(doneAt, Is.InRange(6, 7), "Turn Time of 0.3 s at 0.05 s a tick: done on the sixth tick");
            Assert.IsFalse(turn.CommitsFor(_rig.Context), "Done: it holds the character no more");
        }

        [Test]
        public void TurnToTarget_BehindToTheLeft_ShowsLeft180()
        {
            _target.transform.position = new Vector3(-5f, 0f, 1f);          // −168.7° from +X
            var turn = new TurnToTargetTask { Target = TARGET };

            Step(turn);

            CollectionAssert.AreEqual(new[] { turn.Left180 }, _actions.Played, "Behind and to the left: the big turn left");
        }
    }
}
