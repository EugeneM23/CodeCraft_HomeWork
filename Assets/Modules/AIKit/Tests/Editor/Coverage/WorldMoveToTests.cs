using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Move To an object of the world when somebody stands on its place (step 90; the village: the mayor speaks at the
    /// well, the villagers come to it to chat): the walker presses on and comes no nearer — after Stuck Time next to the
    /// place, still within what the object's scenario reaches, it counts as there, as Perform and Go Inside do (it does
    /// not push at the other one for as long as it stands there). A walk that keeps coming nearer is never cut short.
    /// </summary>
    public sealed class WorldMoveToTests
    {
        private const string OBJECT = "Object";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
        private WorldObjects _registry;

        [SetUp]
        public void SetUp() => _registry = new WorldObjects();

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

        [Test]
        public void MoveTo_APlaceSomebodyStandsOn_ArrivesBesideItAfterTheStuckTime()
        {
            var well = Make<AIObject>("Well", new Vector3(5f, 0f, 0f));
            Assert.AreEqual(0.6f, well.Arrive, "Set-up: the default arrive this test counts with");
            GraphRig rig = Rig(Vector3.zero, out WorldWalker walker);
            walker.BlockAt = well.transform.position;
            walker.BlockWithin = 1f;                                        // the mayor's body: no nearer than a metre
            rig.Context.Blackboard.Set(OBJECT, well.transform);
            var move = new MoveToTask { Target = OBJECT };

            TaskStatus answer = TaskStatus.Running;
            float time = 0f;
            float stoodAt = -1f;
            for (int i = 0; i < 1000 && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = move.Tick(rig.Context, DT);
                time += DT;
                if (stoodAt < 0f && walker.StoodSteps > 0)
                    stoodAt = time;
                rig.Actuator.Apply(DT);
            }

            Assert.Greater(stoodAt, 0f, "Set-up: it came up to the one standing there and stood");
            Assert.AreEqual(TaskStatus.Success, answer, "It counts as there");
            Assert.That(time - stoodAt, Is.EqualTo(Walking.STUCK_TIME).Within(2f * DT),
                $"{Walking.STUCK_TIME} s after it could come no nearer");
            float distance = Flat(rig.Actuator.Position - well.transform.position).magnitude;
            Assert.That(distance, Is.GreaterThan(well.Reach), "Set-up: not within the place's own reach");
            Assert.That(distance, Is.LessThanOrEqualTo(well.Reach + WorldObject.LEAVE_SLACK),
                "Beside it — near enough for the object's scenario (Interact)");
            Assert.AreEqual(NavStatus.Idle, walker.Status, "The walk is called off");
        }

        [Test]
        public void MoveTo_AWalkThatKeepsComingNearer_IsNotCutShort_NorIsOneStoppedFarOff()
        {
            var well = Make<AIObject>("Well", new Vector3(6f, 0f, 0f));
            GraphRig slow = Rig(Vector3.zero, out _);
            slow.Context.Blackboard.Set(OBJECT, well.transform);
            var move = new MoveToTask { Target = OBJECT, Pace = 0.05f };   // 0.2 m/s: long next to the place

            Assert.AreEqual(TaskStatus.Success, Run(slow, move, 40f), "Reached");
            Assert.That(Flat(slow.Actuator.Position - well.transform.position).magnitude,
                Is.LessThanOrEqualTo(well.Reach + 0.01f), "Within the place's own reach: a slow walk is not cut short");

            GraphRig stopped = Rig(Vector3.zero, out WorldWalker walker);
            walker.BlockAt = well.transform.position;
            walker.BlockWithin = well.Reach + WorldObject.LEAVE_SLACK + 1f;     // stands off farther than Interact reaches
            stopped.Context.Blackboard.Set(OBJECT, well.transform);

            Assert.AreEqual(TaskStatus.Running, Run(stopped, new MoveToTask { Target = OBJECT }, 6f),
                "Stood far off: not there (the navigator gives up, not the block)");
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at, out WorldWalker walker)
        {
            var body = new TestActuator();
            walker = new WorldWalker(body);
            var rig = new GraphRig(trace: false, navigator: walker, actuator: body);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            return rig;
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            T component = go.AddComponent<T>();
            if (component is WorldObject worldObject)
                _registry.Register(worldObject);
            return component;
        }

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
