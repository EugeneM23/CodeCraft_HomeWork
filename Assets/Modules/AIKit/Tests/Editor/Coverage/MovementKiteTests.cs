using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static AIKit.Tests.MovementCoverageRigs;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Kite's clocks (step 90 recheck): a retreat backs away all of Retreat Time every time it starts — the time of an
    /// earlier retreat in the same run of the node does not count; a run out of a corner gives up after its limit, and the
    /// agent then shoots for Escape Rest before it runs again (better shoot than run for ever).
    /// </summary>
    public sealed class MovementKiteTests
    {
        private const float DT = 0.02f;
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
            GraphRig.DestroyAll();
        }

        private Transform Bind(GraphRig rig, Vector3 at)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, go.transform);
            return go.transform;
        }

        [Test]
        public void Kite_ASecondRetreatInOneRun_BacksAwayAllOfRetreatTime()
        {
            const float RETREAT = 2f;
            var rig = new GraphRig(trace: false);                            // no walk probe: straight back, no escape
            var sight = new CoverageSight { Clear = false };                 // a column between: no line to shoot along
            rig.Context.Sight = sight;
            Transform target = Bind(rig, new Vector3(3f, 0f, 0f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f, RetreatTime = RETREAT, NeedsSight = true };

            // The first retreat: 3 m → the middle of the band (7 m) at 4 m/s, a second; no line: the node stays on
            for (int i = 0; i < Mathf.CeilToInt(1.5f / DT); i++)
            {
                rig.Context.Advance(DT);
                Assert.AreEqual(TaskStatus.Running, kite.Tick(rig.Context, DT), "No line to the target: it does not shoot");
                rig.Actuator.Apply(DT);
            }

            Assert.That(Flat(rig.Actuator.Position - target.position).magnitude, Is.GreaterThanOrEqualTo(7f - 0.1f),
                "Set-up: it backed away to the middle of the band");

            // The line opens and the target closes in: 3 m behind it, step after step — a second retreat
            sight.Clear = true;
            float retreating = 0f;
            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < Mathf.CeilToInt(4f / DT) && status == TaskStatus.Running; i++)
            {
                Vector3 away = Flat(rig.Actuator.Position - target.position).normalized;
                target.position = rig.Actuator.Position - away * 3f;
                rig.Context.Advance(DT);
                status = kite.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
                retreating += DT;
            }

            Assert.AreEqual(TaskStatus.Success, status, "In Range: it stops and shoots after Retreat Time");
            Assert.That(retreating, Is.EqualTo(RETREAT).Within(DT * 1.5f),
                "The second retreat backs away all of Retreat Time, not what the first one left of it");
        }

        [Test]
        public void Kite_EscapeGivesUpAfterEscapeLimit_ThenRestsEscapeRest()
        {
            // Cornered in the yard's corner, the target between it and the yard: a run to another place, which never
            // arrives (a navigator that keeps walking and never gets there)
            const float ESCAPE_LIMIT = 6f;
            var navigator = new ScriptedWayNavigator();
            var rig = new GraphRig(trace: false, navigator: navigator);
            BoxYard yard = BoxYard.Square(10f);
            rig.Actuator.Position = new Vector3(9.5f, 0f, 9.5f);
            rig.Actuator.Confine = yard.Confine;
            rig.Context.Walk = yard;
            Bind(rig, new Vector3(7f, 0f, 7f));
            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f, RetreatTime = 2f };

            float escapedUntil = float.NaN, nextEscape = float.NaN, shotAt = float.NaN;
            bool wasEscaping = false;
            float time = 0f;
            for (int i = 0; i < Mathf.CeilToInt(14f / DT); i++, time += DT)
            {
                rig.Context.Advance(DT);
                navigator.Advance(DT);
                TaskStatus status = kite.Tick(rig.Context, DT);
                bool escaping = kite.IsEscaping(rig.Context);
                if (wasEscaping && !escaping && float.IsNaN(escapedUntil))
                    escapedUntil = time;
                if (!wasEscaping && escaping && !float.IsNaN(escapedUntil) && float.IsNaN(nextEscape))
                    nextEscape = time;
                if (status == TaskStatus.Success && !float.IsNaN(escapedUntil) && float.IsNaN(shotAt))
                    shotAt = time;
                if (i == 0)
                    Assert.IsTrue(escaping, "Set-up: cornered, it runs for another place at once");

                wasEscaping = escaping;
                rig.Actuator.Apply(DT);
            }

            Assert.That(escapedUntil, Is.EqualTo(ESCAPE_LIMIT).Within(DT * 2f), "A run that never gets there gives up after its limit");
            Assert.That(shotAt, Is.LessThan(escapedUntil + KiteTask.ESCAPE_REST),
                "Between the runs it shoots ('In Range')");
            Assert.That(float.IsNaN(nextEscape) ? float.PositiveInfinity : nextEscape - escapedUntil,
                Is.GreaterThanOrEqualTo(KiteTask.ESCAPE_REST - DT), "No new run for Escape Rest");
        }
    }
}
