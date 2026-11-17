using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Kite watching the way back (Watch The Way Back, the body's walk probe): in the open it backs straight away; with a
    /// wall behind it backs away along the wall instead of rubbing against it; cornered it runs to another place at
    /// shooting distance — past the target if need be — and shoots from there. Off — the old way: into the wall, then it
    /// shoots from too near. A walled yard of 20 × 20 m keeps the test body in (BoxYard).
    /// </summary>
    public sealed class KiteWallTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;
        private const float HALF = 10f;

        private readonly List<Object> _created = new();

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
        public void Kite_TheWayBackIsFree_BacksStraightAwayFromTheTarget()
        {
            GraphRig rig = NewRig(Vector3.zero, out _);
            Transform target = Bind(rig, new Vector3(2f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float sideways = 0f;

            TaskStatus answer = Run(rig, kite, 4f, () => sideways = Mathf.Max(sideways, Mathf.Abs(rig.Actuator.Position.z)));

            Assert.AreEqual(TaskStatus.Success, answer, "In Range");
            Assert.Less(sideways, 0.01f, "Nothing behind it: straight back, the same as without the walk probe");
            Assert.Less(rig.Actuator.Position.x, -1f, "Away from the target");
            Assert.IsFalse(kite.IsEscaping(rig.Context));
        }

        [Test]
        public void Kite_AWallBehind_BacksAwayAlongTheWall_NotIntoIt_AndShootsFromTheBand()
        {
            GraphRig rig = NewRig(new Vector3(HALF - 0.5f, 0f, 0f), out BoxYard yard);
            Transform target = Bind(rig, new Vector3(HALF - 3.5f, 0f, 0f));        // 3 m: too near, the wall at its back
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float min = kite.MinDistance.Get(rig.Context);
            float max = kite.MaxDistance.Get(rig.Context);
            float pressed = 0f;

            TaskStatus answer = Run(rig, kite, 6f, () =>
            {
                if (yard.ToWall(rig.Actuator.Position) < 1e-3f)
                    pressed += DT;
            });

            Assert.AreEqual(TaskStatus.Success, answer, "In Range");
            float distance = Flat(rig.Actuator.Position - target.position).magnitude;
            Assert.That(distance, Is.InRange(min, max), "It shoots from the band, not from too near");
            Assert.Less(pressed, 0.1f, "It never stands pressed against the wall");
            Assert.IsFalse(kite.IsEscaping(rig.Context), "A wall with a free way along it is no corner");
        }

        [Test]
        public void Kite_Cornered_RunsToAnotherPlaceAtShootingDistance_PastTheTarget_AndShootsFromThere()
        {
            Vector3 corner = new(HALF, 0f, HALF);
            GraphRig rig = NewRig(new Vector3(HALF - 0.5f, 0f, HALF - 0.5f), out BoxYard yard);
            Transform target = Bind(rig, new Vector3(HALF - 3f, 0f, HALF - 3f));    // 3.5 m, between it and the yard
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float min = kite.MinDistance.Get(rig.Context);
            float max = kite.MaxDistance.Get(rig.Context);
            bool escaped = false;
            float closest = float.MaxValue;

            TaskStatus answer = Run(rig, kite, 6f, () =>
            {
                escaped |= kite.IsEscaping(rig.Context);
                closest = Mathf.Min(closest, Flat(rig.Actuator.Position - target.position).magnitude);
            });

            Assert.AreEqual(TaskStatus.Success, answer, "In Range");
            Assert.IsTrue(escaped, "Cornered: it ran out to another place");
            Assert.That(Flat(rig.Actuator.Position - target.position).magnitude, Is.InRange(min, max),
                "…at shooting distance");
            Assert.Greater(Flat(rig.Actuator.Position - corner).magnitude, 5f, "…out of the corner");
            Assert.Greater(yard.ToWall(rig.Actuator.Position), -1e-3f);
            Assert.Less(closest, 3f, "Past the target: the way out led by it");
        }

        [Test]
        public void Kite_WatchTheWayBackOff_RubsAgainstTheWall_AndShootsFromTooNear()
        {
            GraphRig rig = NewRig(new Vector3(HALF - 0.5f, 0f, 0f), out _);
            Transform target = Bind(rig, new Vector3(HALF - 3.5f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f, WatchTheWayBack = false };
            float min = kite.MinDistance.Get(rig.Context);

            Assert.AreEqual(TaskStatus.Success, Run(rig, kite, 6f), "In Range after Retreat Time");
            Assert.Less(Flat(rig.Actuator.Position - target.position).magnitude, min,
                "Off — the way it was: backed into the wall, it shoots from too near");
        }

        [Test]
        public void Kite_CorneredWithNoPlaceToRunTo_BacksOffAsBefore_AndShootsAfterRetreatTime()
        {
            // A yard too small for the band: no place at shooting distance anywhere
            var yard = new BoxYard(-2f, 2f, -2f, 2f);
            GraphRig rig = NewRig(new Vector3(1.5f, 0f, 1.5f), out _, yard);
            Bind(rig, new Vector3(0f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f, RetreatTime = 1f };
            float seconds = 0f;

            TaskStatus answer = Run(rig, kite, 4f, () => seconds += DT);

            Assert.AreEqual(TaskStatus.Success, answer, "It shoots anyway: better than standing in the corner forever");
            Assert.That(seconds, Is.EqualTo(kite.RetreatTime.Get(rig.Context)).Within(0.1f));
            Assert.IsFalse(kite.IsEscaping(rig.Context));
        }

        [Test]
        public void Kite_TheTargetKeepsUp_AfterRetreatTime_RunsToAnotherPlace_NotAShotFromTooNear_ThenRests()
        {
            GraphRig rig = NewRig(Vector3.zero, out _, BoxYard.Square(20f));
            Transform target = Bind(rig, new Vector3(2f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f, RetreatTime = 1f };
            float retreatTime = kite.RetreatTime.Get(rig.Context);
            float seconds = 0f;
            float ranAt = -1f;
            TaskStatus answer = Run(rig, kite, retreatTime + 0.5f, () =>
            {
                seconds += DT;
                if (ranAt < 0f && kite.IsEscaping(rig.Context))
                    ranAt = seconds;
                Follow(target, rig.Actuator.Position, 3.8f);         // the target keeps up with it
            });

            Assert.AreEqual(TaskStatus.Running, answer, "No shot from too near: it runs");
            Assert.That(ranAt, Is.EqualTo(retreatTime).Within(2f * DT), "…once it has backed away for Retreat Time");

            // Right after the run the target is on its heels again: a second run waits, it shoots this time
            Run(rig, kite, 6f, () => Follow(target, rig.Actuator.Position, 3.8f),
                () => !kite.IsEscaping(rig.Context));
            Assert.AreEqual(TaskStatus.Success, Run(rig, kite, retreatTime + 0.5f,
                    () => Follow(target, rig.Actuator.Position, 3.8f)),
                "Not run after run: better shoot than run forever");
        }

        // ---------- the rig ----------

        // The target walks at the agent, no nearer than 1.5 m
        private static void Follow(Transform target, Vector3 agent, float speed)
        {
            Vector3 to = Flat(agent - target.position);
            if (to.magnitude > 1.5f)
                target.position += to.normalized * Mathf.Min(speed * DT, to.magnitude - 1.5f);
        }

        private static GraphRig NewRig(Vector3 at, out BoxYard yard, BoxYard own = null)
        {
            var rig = new GraphRig(trace: false);
            yard = own ?? BoxYard.Square(HALF);
            rig.Actuator.Position = at;
            rig.Actuator.Confine = yard.Confine;
            rig.Context.Walk = yard;
            return rig;
        }

        private Transform Bind(GraphRig rig, Vector3 at)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, go.transform);
            return go.transform;
        }

        // The task ticked the way its state ticks it, the body moving as told in between, until it ends or time is out
        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, Action eachTick = null,
            Func<bool> until = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                eachTick?.Invoke();
                rig.Actuator.Apply(DT);
                if (until != null && until())
                    break;
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
