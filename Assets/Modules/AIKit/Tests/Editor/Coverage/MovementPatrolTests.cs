using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Patrol Area (step 90 recheck): picking places on an area object allocates nothing however often it picks; a way
    /// the navigator gives up on the way is dropped and another place is picked the next tick; the pause at each place is
    /// between Pause and Pause Max by the agent's own random — the same seed, the same pauses.
    /// </summary>
    public sealed class MovementPatrolTests
    {
        private const float DT = 0.02f;
        private const string AREA = "Area";
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
        }

        private static AgentContext Patroller(ScriptedWayNavigator navigator, int seed = 1234) =>
            new(null, null, seed) { Actuator = new TestActuator(), Navigation = navigator };

        [Test]
        public void PatrolArea_PickingPlacesOnAnAreaObject_AllocatesNothing()
        {
            // An area object that draws (a zone mesh) partly off the walkable ground: a pick that does not sample onto the
            // mesh is picked again the next tick — a new array of renderers per pick was garbage every tick
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.hideFlags = HideFlags.HideAndDontSave;
            _made.Add(zone);
            var navigator = new ScriptedWayNavigator { Refuses = _ => true };
            AgentContext context = Patroller(navigator);
            context.Blackboard.Set(AREA, zone.transform);
            var patrol = new PatrolAreaTask { Area = AREA };

            for (int i = 0; i < 5; i++)
                patrol.Tick(context, DT);

            int allocated = Allocations.Count(() =>
            {
                for (int i = 0; i < 100; i++)
                    patrol.Tick(context, DT);
            });

            Assert.That(navigator.Asked, Is.GreaterThanOrEqualTo(105), "Set-up: a pick every tick");
            Assert.AreEqual(0, allocated, "Picking places on the area object allocates nothing");
        }

        [Test]
        public void Allocations_ANewArrayACall_IsCounted()
        {
            // The probe the allocation tests of this recheck use sees what it should (GC.GetAllocatedBytesForCurrentThread
            // under the editor's Mono read 0 for this)
            int[] kept = null;
            Assert.That(Allocations.Count(() =>
            {
                for (int i = 0; i < 10; i++)
                    kept = new int[i + 4];
            }), Is.GreaterThanOrEqualTo(10));
            Assert.IsNotNull(kept);
            Assert.AreEqual(0, Allocations.Count(() => { int a = 1; a += 2; }));
        }

        [Test]
        public void PatrolArea_TheWayGivesUpOnTheWay_AnotherPlaceNextTick()
        {
            var navigator = new ScriptedWayNavigator { FollowsFor = 0.5f };
            AgentContext context = Patroller(navigator);
            var patrol = new PatrolAreaTask { Radius = 5f };

            patrol.Tick(context, DT);
            Vector3? first = patrol.CurrentPointFor(context);
            Assert.IsTrue(first.HasValue, "Set-up: a place picked");

            int ticks = 0;
            while (patrol.CurrentPointFor(context).HasValue && ticks++ < 100)
            {
                navigator.Advance(DT);
                patrol.Tick(context, DT);
            }

            Assert.That(ticks * DT, Is.EqualTo(0.5f).Within(DT * 1.5f), "Dropped on the tick the navigator gave up");
            Assert.IsNull(patrol.CurrentPointFor(context));

            patrol.Tick(context, DT);
            Vector3? next = patrol.CurrentPointFor(context);
            Assert.IsTrue(next.HasValue, "The next tick picks another place");
            Assert.AreNotEqual(first.Value, next.Value);
        }

        // The pauses at five places in a row on this seed (from the arrival to the next pick)
        private static List<float> FivePauses(int seed)
        {
            var navigator = new ScriptedWayNavigator();
            AgentContext context = Patroller(navigator, seed);
            var patrol = new PatrolAreaTask { Pause = 1f, PauseMax = 2f, ArriveDistance = 1000f, Radius = 5f };
            var pauses = new List<float>();
            float since = -1f;
            bool had = false;
            for (int i = 0; i < 2000 && pauses.Count < 5; i++)
            {
                patrol.Tick(context, DT);
                bool has = patrol.CurrentPointFor(context).HasValue;
                if (had && !has)
                    since = 0f;                                 // arrived: the pause begins
                else if (!had && has && since >= 0f)
                {
                    pauses.Add(since);
                    since = -1f;
                }
                else if (since >= 0f)
                {
                    since += DT;
                }

                had = has;
            }

            return pauses;
        }

        [Test]
        public void PatrolArea_PausesBetweenPauseAndPauseMax_FromTheAgentsRandom()
        {
            List<float> first = FivePauses(7);
            List<float> again = FivePauses(7);

            Assert.AreEqual(5, first.Count, "Set-up: five places walked to");
            Assert.That(first, Is.All.InRange(1f - DT, 2f + 2f * DT), "Each pause between Pause and Pause Max");
            CollectionAssert.AreEqual(first, again, "The same seed: the same pauses");
        }
    }
}
