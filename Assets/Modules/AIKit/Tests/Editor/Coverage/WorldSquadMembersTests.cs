using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Who marches in an AI Squad (step 88; handbook §20a.2 — a fallen man's place is taken by the one behind him): the
    /// squad is its list, followed every step — a soldier taken off the list leaves the squad at once (its slot goes to
    /// the one behind it, and it keeps no formation of a squad that does not list it), and so does one destroyed by the
    /// game; the squad's numbers (who is near the foe, how many stand in their slots) count only those who march. The
    /// running command graph sees a binding the moment it is made.
    /// </summary>
    public sealed class WorldSquadMembersTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;
        private const int FILES = 10;
        private const float SPACING = 1.8f;
        private const float RANK_GAP = 2f;

        private readonly List<GameObject> _created = new();
        private readonly List<GraphRig> _rigs = new();
        private AIWorld _world;
        private AISquad _squad;
        private BehaviorGraph _memberGraph;

        [SetUp]
        public void SetUp()
        {
            _world = new AIWorld();
            _memberGraph = MemberGraph();
        }

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
        public void AISquad_AMemberTakenOffItsList_LeavesTheSquad_TheOneBehindStepsUp()
        {
            MakeSquad(Vector3.zero);
            Formed();
            Run(2f);
            Squad squad = _squad.Squad;
            GraphRig removed = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4);
            GraphRig behind = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4 + FILES);
            Assert.IsTrue(squad.SlotOf(removed.Context, out Vector3 hole, out _, out _, out _), "Set-up: in the front rank");

            _squad.Members.Remove(removed.Agent);                           // taken off the list; it stays switched on
            Run(4f);

            Assert.IsFalse(squad.Has(removed.Context), "It left the squad");
            Assert.IsNull(removed.Context.Squad, "…it marches in no squad");
            Assert.AreEqual(2 * FILES - 1, squad.Count);
            Assert.AreEqual(4, squad.SlotIndexOf(behind.Context), "The one behind took its place");
            Assert.That(Flat(behind.Context.Position - hole).magnitude, Is.LessThan(0.6f), "…and stands there");
            Assert.IsNotInstanceOf<FollowSquadReasoner>(removed.Instance.Decision?.Reasoner,
                "It keeps no formation of a squad that does not list it");
        }

        [Test]
        public void AISquad_AMemberDestroyed_LeavesTheSquad()
        {
            MakeSquad(new Vector3(30f, 0f, 30f));
            Formed();
            Run(2f);
            Squad squad = _squad.Squad;
            GraphRig gone = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4);
            GraphRig behind = _rigs.First(rig => squad.SlotIndexOf(rig.Context) == 4 + FILES);
            Transform foe = Make("Foe", Vector3.zero).transform;

            Object.DestroyImmediate(gone.Agent.gameObject);                // the game destroys a fallen soldier
            Run(4f);

            Assert.AreEqual(2 * FILES - 1, squad.Count, "It left the squad");
            Assert.AreEqual(4, squad.SlotIndexOf(behind.Context), "Its place went to the one behind it");
            Assert.That(squad.Cohesion, Is.GreaterThanOrEqualTo(0.95f), "The living stand in their slots");
            float nearest = _rigs.Where(rig => rig.Agent != null)
                .Min(rig => Flat(foe.position - rig.Context.Position).magnitude);
            Assert.That(squad.DistanceTo(foe), Is.EqualTo(nearest).Within(1e-3f),
                "How near the foe the squad is counts only the living");
        }

        [Test]
        public void AISquad_Disabled_EveryMemberLeaves_TheListedAndTheOnesTakenOffIt()
        {
            MakeSquad(Vector3.zero);
            Formed();
            Run(1f);
            Squad squad = _squad.Squad;
            GraphRig taken = _rigs[3];
            _squad.Members.Remove(taken.Agent);                             // off the list, and the squad stops before its step

            _squad.enabled = false;
            SwitchedOff(_squad);

            Assert.AreEqual(0, squad.Count, "Nobody marches in a squad that is switched off");
            Assert.IsTrue(_rigs.All(rig => rig.Context.Squad == null), "…every one of them is in no squad");
        }

        [Test]
        public void AISquad_Bind_TheRunningCommandGraphSeesItAtOnce()
        {
            MakeSquad(Vector3.zero);
            _squad.Initialize();
            Transform place = Make("Meeting Place", new Vector3(4f, 0f, 4f)).transform;

            _squad.Bind("Meeting Place", place);
            Assert.AreSame(place, _squad.Context.Blackboard.Get<Transform>("Meeting Place"), "Seen at once");
            Assert.AreSame(place, _squad.BoundTo("Meeting Place"));

            _squad.Bind("Meeting Place", null);
            Assert.IsNull(_squad.Context.Blackboard.Get<Transform>("Meeting Place"), "Unbound: gone at once");
            Assert.IsNull(_squad.BoundTo("Meeting Place"));
            Assert.AreEqual(1, _squad.Bindings.Count, "One binding of the variable, not two");
        }

        // ---------- the squad ----------

        private void MakeSquad(Vector3 banner)
        {
            _squad = Make("Squad", banner).AddComponent<AISquad>();
            _squad.ThinkInterval = 0f;
            _squad.SetGraph(CommandGraph());
        }

        // The squad's command graph: Squad Sight → Squad Brain → its four decisions with their blocks
        private static BehaviorGraph CommandGraph()
        {
            var g = new BrainGraphBuilder(new SquadBrainTask
            {
                Target = TARGET, Files = FILES, Spacing = SPACING, RankGap = RANK_GAP, StrikersPerFoe = 3
            });
            g.Sensor(new SquadSightTask { StoreIn = TARGET, Interval = 0f });
            g.Variable(TARGET, VariableType.Transform);
            g.Chain(g.Decision(new SurroundDecision(), 1), new FormRingTask());
            g.Chain(g.Decision(new HoldLineDecision(), 2), new HoldLineTask());
            g.Chain(g.Decision(new AdvanceDecision(), 3), new MarchToTargetTask());
            g.Chain(g.Decision(new FormUpDecision(), 4), new ReturnToBannerTask());
            return g.Build();
        }

        // A member's own graph: Follow Squad → Keep Formation → (In Reach) Strike → back
        private static BehaviorGraph MemberGraph()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState keep = g.Chain(g.Decision(new FollowSquadReasoner(), 5), new KeepFormationTask())[0];
            AgentState strike = g.Block(new AttackTask());
            g.Then(keep, strike);
            g.Then(strike, keep);
            g.OnFail(strike, keep);
            return g.Build();
        }

        private GraphRig Member(Vector3 at)
        {
            var rig = new GraphRig(trace: false, world: _world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.GiveWeapon(3.4f, 0.3f, 0.5f);
            rig.SetGraph(_memberGraph);
            rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            _rigs.Add(rig);
            _squad.Members.Add(rig.Agent);
            return rig;
        }

        // Twenty in their ranks at the banner already: two ranks of ten facing its way
        private void Formed()
        {
            Vector3 banner = _squad.transform.position;
            for (int rank = 0; rank < 2; rank++)
            {
                for (int file = 0; file < FILES; file++)
                    Member(banner + new Vector3((file - (FILES - 1) * 0.5f) * SPACING, 0f, -rank * RANK_GAP));
            }
        }

        // The world's step, the squad's, then every member's that is there and switched on
        private void Run(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps; i++)
            {
                _world.BeginStep(DT);
                _squad.Step(DT);
                foreach (GraphRig rig in _rigs)
                {
                    if (rig.Agent == null || !rig.Agent.enabled)
                        continue;

                    rig.Step(DT);
                    rig.Agent.transform.position = rig.Actuator.Position;
                }
            }
        }

        // What Unity calls when a component is switched off in Play (Edit Mode sends this component no messages)
        private static void SwitchedOff(MonoBehaviour component)
        {
            MethodInfo onDisable = component.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, $"{component.GetType().Name}.OnDisable is gone: the test calls it");
            onDisable.Invoke(component, null);
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
