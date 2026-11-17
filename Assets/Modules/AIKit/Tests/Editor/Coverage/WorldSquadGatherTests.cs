using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A squad called together (step 89 — the village's Town Meeting; handbook §21 — the coordinator moves the group,
    /// each member keeps its slot by its own graph): Gather holds while its flag is on; Form Ring lays the members out in
    /// a ring round the meeting place, wide enough for them (never narrower than its Radius), with the one named Middle in
    /// the centre; once they have gathered — nine in ten on their places and the middle one on his, or Gather Limit seconds
    /// after the rings were formed, whoever is still missing — the ring stands Stand For seconds and the flag goes off,
    /// and every member lets go of the formation Led For after the squad stops leading.
    /// </summary>
    public sealed class WorldSquadGatherTests
    {
        private const string CALLED = "Called";
        private const string PLACE = "Meeting Place";
        private const string MAYOR = "Mayor";
        private const int FILES = 32;
        private const float SPACING = 1.4f;
        private const float RANK_GAP = 1.6f;
        private const float RADIUS = 4.5f;
        private const float STAND_FOR = 2f;

        private readonly List<GameObject> _created = new();
        private readonly List<GraphRig> _rigs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            _rigs.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Gather_RingRoundThePlace_TheMiddleMemberInItsCentre_StandsFor_ThenTheFlagIsOff()
        {
            const float DT = 0.02f;
            var world = new AIWorld();
            var squad = Make("Town Meeting", new Vector3(0f, 0f, -12f)).AddComponent<AISquad>();
            var ring = new FormRingTask
            {
                Target = PLACE, Middle = MAYOR, Radius = RADIUS, FollowSpeed = 2f, StandFor = STAND_FOR
            };
            squad.SetGraph(MeetingGraph(ring));
            Transform place = Make(PLACE, new Vector3(2f, 0f, 3f)).transform;
            var random = new System.Random(5);
            for (int i = 0; i < 21; i++)
            {
                var at = new Vector3((float)random.NextDouble() * 20f - 10f, 0f, (float)random.NextDouble() * 20f - 10f);
                Member(world, squad, at);
            }

            GraphRig mayor = _rigs[0];
            squad.Bind(PLACE, place);
            squad.Bind(MAYOR, mayor.Agent.transform);
            squad.Context.Blackboard.Set(CALLED, true);

            float time = 0f;
            float offAt = -1f;
            float stoodWhenOff = 0f;
            List<float> offRing = null;
            var letGo = new Dictionary<GraphRig, float>();
            for (int i = 0; i < 6000 && (offAt < 0f || letGo.Count < _rigs.Count); i++)
            {
                world.BeginStep(DT);
                squad.Step(DT);
                foreach (GraphRig rig in _rigs)
                {
                    rig.Step(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }

                time += DT;
                if (offAt < 0f && !squad.Context.Blackboard.Get<bool>(CALLED))
                {
                    offAt = time;
                    stoodWhenOff = ring.StoodFor(squad.Context);
                    offRing = _rigs.Where(rig => rig != mayor)
                        .Select(rig => Mathf.Abs(Flat(rig.Context.Position - place.position).magnitude - squad.Squad.RingRadius))
                        .ToList();
                    Assert.IsTrue(squad.Squad.SlotOf(mayor.Context, out Vector3 middle, out _, out _, out _));
                    Assert.That(Flat(middle - place.position).magnitude, Is.LessThan(1e-3f), "The mayor's place: the middle");
                    Assert.That(Flat(mayor.Context.Position - place.position).magnitude, Is.LessThan(Squad.IN_PLACE),
                        "…and he stands there");
                }

                if (offAt < 0f)
                    continue;

                foreach (GraphRig rig in _rigs)
                {
                    if (!letGo.ContainsKey(rig) && rig.Instance.Decision?.Reasoner is not FollowSquadReasoner)
                        letGo[rig] = time;
                }
            }

            int onTheRing = _rigs.Count - 1;                                 // the mayor stands in the middle
            Assert.AreEqual(Mathf.Max(RADIUS, Mathf.Min(FILES, onTheRing) * SPACING / (2f * Mathf.PI)),
                squad.Squad.RingRadius, 1e-4f, "The ring: its Radius (twenty at 1.4 m apart would need only 4.46 m)");
            Assert.AreEqual(RADIUS, squad.Squad.RingRadius, 1e-4f);
            Assert.Greater(offAt, 0f, "The flag went off");
            Assert.That(stoodWhenOff, Is.EqualTo(STAND_FOR).Within(0.1f), "…after the ring stood Stand For");
            Assert.That(offRing, Is.All.LessThanOrEqualTo(1.2f), "Every other member on the ring");
            Assert.AreEqual(_rigs.Count, letGo.Count, "Every member let go of the formation");
            float latest = offAt + Squad.LED_FOR + squad.ThinkInterval + _rigs[0].ThinkInterval + 2f * DT;
            Assert.That(letGo.Values, Is.All.LessThanOrEqualTo(latest),
                "…no later than Led For after the squad stopped leading (plus a think of each)");
        }

        [Test]
        public void FormRing_OneNeverComes_GatheredAfterThirtySeconds_ThenStandsFor()
        {
            const float DT = 0.05f;
            var squad = new Squad(Make("Banner", Vector3.zero).transform);
            squad.Configure(FILES, SPACING, RANK_GAP, 3);
            var members = new List<GraphRig>();
            for (int i = 0; i < 21; i++)
            {
                var rig = new GraphRig(trace: false);
                rig.WakeUpAt(new Vector3(i - 10f, 0f, -8f));
                squad.Join(rig.Context);
                members.Add(rig);
            }

            var command = new AgentContext(null, new Blackboard()) { Leads = squad };
            Transform place = Make(PLACE, new Vector3(0f, 0f, 6f)).transform;
            command.Blackboard.Set(PLACE, place);
            command.Blackboard.Set(MAYOR, members[0].Agent.transform);
            var ring = new FormRingTask { Target = PLACE, Middle = MAYOR, Radius = RADIUS, StandFor = STAND_FOR };
            List<GraphRig> away = members.Skip(16).ToList();                 // five never come

            float time = 0f;
            float gatheredAt = -1f;
            float doneAt = -1f;
            for (int i = 0; i < 1000 && doneAt < 0f; i++)
            {
                squad.Begin(DT);
                TaskStatus status = ring.Tick(command, DT);
                squad.End();
                time += DT;
                foreach (GraphRig member in members.Except(away))
                {
                    squad.SlotOf(member.Context, out Vector3 slot, out _, out _, out _);
                    member.Actuator.Position = slot;                         // these keep their places
                }

                if (gatheredAt < 0f && ring.IsGathered(command))
                    gatheredAt = time;
                if (status == TaskStatus.Success)
                    doneAt = time;
                Assert.That(squad.Cohesion, Is.LessThan(FormRingTask.GATHERED), "Set-up: five of 21 never come");
            }

            Assert.That(gatheredAt, Is.EqualTo(FormRingTask.GATHER_LIMIT).Within(DT + 1e-3f),
                "Never nine in ten: gathered all the same Gather Limit seconds after the rings were formed");
            Assert.That(doneAt, Is.EqualTo(FormRingTask.GATHER_LIMIT + STAND_FOR).Within(DT + 1e-3f),
                "…then it stands Stand For and is done");
        }

        // ---------- helpers ----------

        // The village's command graph: Squad Brain → Gather (its Called flag) → Form Ring round the place, the mayor in
        // the middle, Stand For → Set Flag Called off
        private static BehaviorGraph MeetingGraph(FormRingTask ring)
        {
            var g = new BrainGraphBuilder(new SquadBrainTask
            {
                Files = FILES, Spacing = SPACING, RankGap = RANK_GAP, StrikersPerFoe = 3
            });
            g.Variable(new VariableDeclaration(CALLED, VariableType.Bool));
            g.Variable(PLACE, VariableType.Transform);
            g.Variable(MAYOR, VariableType.Transform);
            AgentState gathering = g.Decision(new GatherDecision { Condition = new CompareCondition(CALLED, VariableType.Bool) }, 1);
            g.Chain(gathering, ring, new SetFlagTask { Variable = CALLED, Value = false });
            return g.Build();
        }

        // A villager: Follow Squad → Keep Formation at a walk
        private static BehaviorGraph MemberGraph()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new FollowSquadReasoner(), 1), new KeepFormationTask { Pace = 0.5f });
            return g.Build();
        }

        private void Member(AIWorld world, AISquad squad, Vector3 at)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.SetGraph(MemberGraph());
            _rigs.Add(rig);
            squad.Members.Add(rig.Agent);
        }

        private GameObject Make(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
