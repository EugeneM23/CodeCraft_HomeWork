using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The home of a patrol (step 90; handbook §25б.1 — home is the spawn point): Patrol Area without an area walks
    /// around where the character woke up — fixed at its first step, not where the patrol began, which after a chase
    /// would be wherever the chase ended. An area is optional: an AI Object with a zone is walked within its zone.
    /// </summary>
    public sealed class PatrolHomeTests
    {
        private const string AREA = "Area";
        private const float DT = 0.02f;

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
        public void Home_FixedAtTheFirstStep_ThenNeverMoved()
        {
            var rig = new GraphRig(trace: false);
            var wokeUp = new Vector3(4f, 0f, -2f);
            rig.Actuator.Position = wokeUp;
            Assert.IsFalse(rig.Context.HasHome, "Not woken up yet");

            rig.Step(DT);
            Assert.IsTrue(rig.Context.HasHome);
            Assert.AreEqual(wokeUp, rig.Context.Home, "Where it was at its first step");

            rig.Actuator.Position = new Vector3(40f, 0f, 0f);
            rig.Step(DT, 10);
            Assert.AreEqual(wokeUp, rig.Context.Home, "Walking elsewhere moves no home");
        }

        [Test]
        public void PatrolArea_NoArea_AfterAChase_WalksAroundWhereItWokeUp_NotWhereThePatrolBeganAgain()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            var wokeUp = new Vector3(-5f, 0f, 3f);
            rig.Actuator.Position = wokeUp;
            var chasing = new Flag();
            var patrol = new PatrolAreaTask { Radius = 3f, Pause = 0f, PauseMax = 0.1f };
            float radius = patrol.Radius.Get(null);
            var g = new BrainGraphBuilder();
            AgentState chase = g.Decision(new DailyRoutineReasoner { Condition = chasing }, 1);
            g.Do(chase, g.Block(new MoveToTask(new Vector3(30f, 0f, 0f), 0.3f)));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(patrol));
            rig.SetGraph(g.Build());

            rig.StepSeconds(4f);                                    // a patrol at home
            chasing.Value = true;                                   // off after something, far away
            Assert.IsTrue(rig.StepUntil(() => (rig.Actuator.Position - new Vector3(30f, 0f, 0f)).magnitude < 0.5f, 20f, DT),
                "Set-up: the chase took it 35 m away");
            chasing.Value = false;

            var places = new List<Vector3>();
            for (int i = 0; i < Mathf.CeilToInt(30f / DT); i++)
            {
                rig.Step(DT);
                Vector3? place = patrol.CurrentPointFor(rig.Context);
                if (place.HasValue && !places.Contains(place.Value))
                    places.Add(place.Value);
            }

            Assert.GreaterOrEqual(places.Count, 3, "It patrols again");
            foreach (Vector3 place in places)
                Assert.LessOrEqual(Flat(place - wokeUp).magnitude, radius + 1e-3f, "Every place round its home");
            Assert.LessOrEqual(Flat(rig.Actuator.Position - wokeUp).magnitude, radius + 0.5f, "…and it walked back there");
        }

        [Test]
        public void PatrolArea_AnAIObjectWithAZone_EveryPlaceInItsZone()
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(Vector3.zero);
            var go = new GameObject("Patrol Zone") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.SetPositionAndRotation(new Vector3(12f, 0f, 6f), Quaternion.Euler(0f, 30f, 0f));
            var zone = go.AddComponent<AIObject>();
            zone.Kind = "Patrol Zone";
            zone.ZoneSize = new Vector2(6f, 2f);
            rig.Context.Blackboard.Set(AREA, go.transform);
            var patrol = new PatrolAreaTask { Area = AREA, Radius = 50f, Pause = 0f, PauseMax = 0.1f };

            var places = new List<Vector3>();
            for (int i = 0; i < Mathf.CeilToInt(30f / DT); i++)
            {
                rig.Context.Advance(DT);
                patrol.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
                Vector3? place = patrol.CurrentPointFor(rig.Context);
                if (place.HasValue && !places.Contains(place.Value))
                    places.Add(place.Value);
            }

            Assert.GreaterOrEqual(places.Count, 5);
            foreach (Vector3 place in places)
                Assert.IsTrue(zone.Contains(place, 1e-3f), $"{place}: inside the zone (a turned box), not in the radius");
            Assert.IsTrue(zone.Contains(go.transform.position + go.transform.right * 2.9f, 0f), "The box turns with it");
            Assert.IsFalse(zone.Contains(go.transform.position + Vector3.right * 2.9f + Vector3.forward * 1.2f, 0f));
        }

        private sealed class Flag : AgentCondition
        {
            public bool Value;

            protected override bool Evaluate(AgentContext context) => Value;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
