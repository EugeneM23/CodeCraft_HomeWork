using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stand on a real agent (handbook §20b.4 p. 6: a pause between the series): the body stays where it is, turned to
    /// its target, for a time between Time and Time Max, then Done; with no target it still stands its time.
    /// </summary>
    public sealed class StandTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _rig.Context.Blackboard.Set(TARGET, _target.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            GraphRig.DestroyAll();
        }

        private TaskStatus Step(AgentTask task)
        {
            _rig.Context.Advance(DT);
            TaskStatus status = task.Tick(_rig.Context, DT);
            _rig.Actuator.Apply(DT);
            return status;
        }

        // Seconds until the task is done
        private float Run(AgentTask task)
        {
            float time = 0f;
            for (int i = 0; i < 400; i++)
            {
                time += DT;
                if (Step(task) != TaskStatus.Running)
                    return time;
            }

            return float.PositiveInfinity;
        }

        [Test]
        public void Stand_StaysPut_TurnedToTheTarget_ForItsTime_ThenDone()
        {
            _target.transform.position = new Vector3(0f, 0f, 5f);         // to the left of the body's nose (+X)
            Vector3 start = _rig.Actuator.Position;
            var stand = new StandTask { Target = TARGET, Time = 1f };

            float lasted = Run(stand);

            Assert.AreEqual(1f, lasted, DT + 1e-4f, "Stands for Time");
            Assert.AreEqual(TaskStatus.Success, stand.StatusOf(_rig.Context));
            Assert.Less(Vector3.Distance(start, _rig.Actuator.Position), 1e-3f, "Never moves a step");
            Vector3 to = _target.transform.position - _rig.Actuator.Position;
            to.y = 0f;
            Assert.Less(Vector3.Angle(_rig.Actuator.Facing, to), 2f, "Turned to its target");
        }

        [Test]
        public void Stand_TimeToTimeMax_EachStandIsWithinThem_NotAlwaysTheSame()
        {
            var stand = new StandTask { Target = TARGET, Time = 0.5f, TimeMax = 1.5f };
            var lasted = new List<float>();
            for (int i = 0; i < 12; i++)
                lasted.Add(Run(stand));

            foreach (float time in lasted)
                Assert.That(time, Is.InRange(0.5f - 1e-4f, 1.5f + DT + 1e-4f));
            Assert.Greater(Mathf.Max(lasted.ToArray()) - Mathf.Min(lasted.ToArray()), 0.3f, "A pause of its own every time");
        }

        [Test]
        public void Stand_NoTarget_StillStandsItsTime()
        {
            var stand = new StandTask { Target = "Nobody", Time = 0.5f };

            Assert.AreEqual(0.5f, Run(stand), DT + 1e-4f);
            Assert.AreEqual(TaskStatus.Success, stand.StatusOf(_rig.Context));
        }
    }
}
