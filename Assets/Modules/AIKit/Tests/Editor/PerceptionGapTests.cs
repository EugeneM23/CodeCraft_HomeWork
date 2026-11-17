using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The corners of the senses: perception without a scanner or a body, the crowd limit of tracks, how a tie between
    /// two equally known targets is broken, search points without a memory or on a turned body, the list scanner and
    /// the perception inputs of a consideration.
    /// </summary>
    public class PerceptionGapTests
    {
        private const float DT = 0.02f;

        private readonly List<GraphRig> _rigs = new();
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GraphRig rig in _rigs)
                rig.DestroyTarget();
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _rigs.Clear();
            _created.Clear();
        }

        private Transform NewTransform(string name, Vector3 position)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        private GraphRig Rig(int seed = 1234, PerceptionProfile profile = null)
        {
            var rig = new GraphRig(seed, trace: false);
            _rigs.Add(rig);
            rig.GivePerception(profile);
            return rig;
        }

        /// <summary>A rig that saw a target at `lastSeen` and remembers it (the target is not ticked any more).</summary>
        private GraphRig RigWithMemory(Vector3 lastSeen, int seed = 1234)
        {
            GraphRig rig = Rig(seed);
            Transform target = rig.CreateTargetAt(lastSeen);
            rig.Context.Perception.Alert(target);
            return rig;
        }

        // ---------- perception without its parts ----------

        [Test]
        public void Perception_NoScanner_LooksAndSeesNothing()
        {
            GraphRig rig = Rig();
            AgentPerception perception = rig.Context.Perception;
            perception.Scanner = null;
            rig.CreateTargetAt(new Vector3(3f, 0f, 0f));

            perception.Step(DT);

            Assert.AreEqual(1, perception.Looks, "It looked");
            Assert.AreEqual(0, perception.Tracks.Count, "Without a scanner nobody is around");
            Assert.AreEqual(0f, perception.NextLookIn, "Profile without an interval: it looks every step");
        }

        [Test]
        public void Perception_NoBody_LooksAlongTheWorldRight()
        {
            var context = new AgentContext(null, null, 3);
            var scanner = new GraphRig.RigScanner();
            var profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.BackRange = 0f;
            });
            var perception = new AgentPerception(context, profile, scanner);
            Transform ahead = NewTransform("Ahead", new Vector3(5f, profile.EyeHeight, 0f));
            Transform behind = NewTransform("Behind", new Vector3(-5f, profile.EyeHeight, 0f));
            scanner.Candidates.Add(ahead);
            scanner.Candidates.Add(behind);

            perception.Step(DT);

            Assert.IsTrue(perception.IsVisible(ahead), "In front: +x");
            Assert.IsFalse(perception.IsVisible(behind), "Behind, out of the back range");
        }

        [Test]
        public void Perception_ItsOwnEntity_IsNeverATarget()
        {
            GraphRig rig = Rig();
            rig.Scanner.Candidates.Add(rig.Agent.EntityRoot);

            rig.Context.Perception.Step(DT);

            Assert.AreEqual(0, rig.Context.Perception.Tracks.Count);
        }

        [Test]
        public void Perception_Crowd_KeepsTheBestKnownTracksOnly()
        {
            GraphRig rig = Rig();
            AgentPerception perception = rig.Context.Perception;
            float[] awareness = { 0.9f, 0.5f, 0.8f, 0.7f, 0.6f };
            var faces = new List<Transform>();
            for (int i = 0; i < awareness.Length; i++)
            {
                faces.Add(NewTransform($"Face{i}", new Vector3(2f + i, 0f, 0f)));
                perception.Alert(faces[i], awareness[i]);
            }

            Assert.AreEqual(4, perception.Tracks.Count, "A bounded number of tracks");
            Assert.IsFalse(perception.IsVisible(faces[1]), "The least known was forgotten to make room");
            foreach (int kept in new[] { 0, 2, 3, 4 })
                Assert.IsTrue(perception.IsVisible(faces[kept]), $"Face{kept} kept");
        }

        [Test]
        public void Perception_TiedAwareness_TheNearerIsTheInterest()
        {
            GraphRig rig = Rig();
            AgentPerception perception = rig.Context.Perception;
            Transform far = NewTransform("Far", new Vector3(9f, 0f, 0f));
            Transform near = NewTransform("Near", new Vector3(3f, 0f, 0f));
            Transform weak = NewTransform("Weak", new Vector3(1f, 0f, 0f));

            perception.Alert(far);
            perception.Alert(near);
            perception.Alert(weak, 0.5f);

            Assert.AreSame(near, perception.Best.Transform, "Equally known: the nearer; less known: never");
        }

        [Test]
        public void Perception_TiedAwareness_TheOneInSightWinsOverTheNearer()
        {
            GraphRig rig = Rig(profile: PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 180f;
                p.DecayPerSecond = 0f;
            }));
            AgentPerception perception = rig.Context.Perception;
            Transform hidden = NewTransform("Hidden", new Vector3(2f, 0f, 0f));
            Transform seen = NewTransform("Seen", new Vector3(8f, 0f, 0f));
            rig.Scanner.Candidates.Add(seen);                   // the scanner finds only this one
            perception.Alert(hidden);
            perception.Alert(seen);

            perception.Step(DT);

            Assert.AreEqual(perception.Tracks[0].Awareness, perception.Tracks[1].Awareness, "A tie");
            Assert.AreSame(seen, perception.Best.Transform);
        }

        [Test]
        public void Perception_NothingPerceived_DropsAHandedOverTarget()
        {
            GraphRig rig = Rig();
            rig.Context.Target = NewTransform("HandedOver", new Vector3(3f, 0f, 0f));
            Assume.That(rig.Context.HasTarget);

            rig.Context.Perception.Step(DT);

            Assert.IsFalse(rig.Context.HasTarget, "Perception is the only writer of the target");
            Assert.AreEqual(AlertLevel.Idle, rig.Context.Perception.Level);
        }

        [Test]
        public void SearchPoints_NothingKnown_Empty()
        {
            GraphRig rig = Rig();
            var points = new List<Vector3> { Vector3.one };

            Assert.AreEqual(0, rig.Context.Perception.SearchPoints(3, 2f, points));
            Assert.IsEmpty(points);
        }

        [Test]
        public void SearchPoints_BodyUpAlongDepth_PointsStayWithinTheRadius()
        {
            var lastSeen = new Vector3(4f, 0f, 0f);
            GraphRig rig = RigWithMemory(lastSeen);
            rig.Actuator.UpDirection = Vector3.forward;
            var points = new List<Vector3>();

            Assert.AreEqual(3, rig.Context.Perception.SearchPoints(3, 2f, points));

            foreach (Vector3 point in points)
            {
                Assert.LessOrEqual(Vector3.Distance(point, lastSeen), 2f + 1e-4f);
                Assert.AreEqual(0f, Vector3.Dot(point - lastSeen, Vector3.forward), 1e-4f, "On the plane across the up");
            }
        }

        // ---------- the list scanner ----------

        [Test]
        public void ListScanner_FindsTheListedWithinTheRadius_ActiveOnly()
        {
            var scanner = new ListTargetScanner();
            Transform near = NewTransform("Near", new Vector3(2f, 0f, 0f));
            Transform far = NewTransform("Far", new Vector3(20f, 0f, 0f));
            Transform off = NewTransform("Off", new Vector3(1f, 0f, 0f));
            off.gameObject.SetActive(false);
            scanner.Candidates.AddRange(new[] { near, far, off, null });
            var results = new List<Transform> { far };

            int found = scanner.Scan(Vector3.zero, 5f, results);

            Assert.AreEqual(1, found);
            CollectionAssert.AreEqual(new[] { near }, results);
        }

        [Test]
        public void ListScanner_AsTheBodysEyes_GivesTheSensesSensorItsCandidates()
        {
            GameObject owner = NewTransform("Character", Vector3.zero).gameObject;
            owner.SetActive(false);
            var agent = owner.AddComponent<AICharacter>();
            var body = new ListEyesBody();
            body.Candidates.Add(NewTransform("Enemy", new Vector3(3f, 0f, 0f)));
            agent.Body = body;
            agent.Simulation = AICharacter.SimulationMode.Manual;
            agent.Initialize();
            var g = new BrainGraphBuilder();
            g.Variable("Target", VariableType.Transform);
            g.Sensor(new SensesSensorTask
            {
                StoreIn = "Target",
                Profile = PerceptionProfile.Create(p =>
                {
                    p.VisionIntervalMin = 0f;
                    p.VisionIntervalMax = 0f;
                })
            });
            g.Chain(g.Always(), new WaitTask(100f));
            agent.SetGraph(g.Build());

            agent.Step(DT);                                     // the graph starts: its Senses sensor opens the senses
            agent.Sense(DT);

            Assert.IsNotNull(agent.Context.Perception);
            Assert.AreEqual(1, agent.Context.Perception.Tracks.Count, "The listed enemy is perceived");
        }

        /// <summary>A body without physics that sees only the listed transforms (the demo's body is the same).</summary>
        private sealed class ListEyesBody : AgentBody
        {
            public readonly List<Transform> Candidates = new();
            private TransformMover _mover;
            private ListTargetScanner _eyes;

            public override void Attach(Transform owner)
            {
                _mover = new TransformMover(owner, new SimpleBody());
                _eyes = new ListTargetScanner(Candidates);
            }

            public override IAgentActuator Actuator => _mover;

            public override ITargetScanner Scanner => _eyes;
        }

        // ---------- perception inputs of a consideration ----------

        [Test]
        public void Consideration_RequiresTarget_NoTarget_NoData()
        {
            var context = new AgentContext(null, null, 1);
            var axis = new Consideration { Input = ConsiderationInput.Constant, Constant = 1f, RequiresTarget = true };

            Assert.IsFalse(axis.Evaluate(context, out _));
            context.Target = NewTransform("Target", Vector3.right);
            Assert.IsTrue(axis.Evaluate(context, out UtilityScore score));
            Assert.AreEqual(1f, (float)score, 1e-5f);
        }

        [Test]
        public void Consideration_Random_SameSeedSameRoll_NoAgentHalfway()
        {
            var axis = new Consideration { Input = ConsiderationInput.Random };
            float a = axis.Evaluate(new AgentContext(null, null, 5));
            float b = axis.Evaluate(new AgentContext(null, null, 5));

            Assert.AreEqual(a, b, "One seed, one roll");
            Assert.That(a, Is.InRange(0f, 1f));
            Assert.AreEqual(0.5f, axis.Evaluate(null), 1e-5f, "Without an agent: the middle");
        }

        [Test]
        public void Consideration_Awareness_ReadsTheTrack()
        {
            var context = new AgentContext(null, null, 1);
            var axis = new Consideration { Input = ConsiderationInput.Awareness };
            context.Track.Awareness = 0.4f;

            Assert.AreEqual(0.4f, axis.Evaluate(context), 1e-5f);
            Assert.IsFalse(axis.Evaluate(null, out _), "No agent: no data");
        }

        [Test]
        public void Consideration_TimeSinceSeen_NeverSeenNoData_InSightZero_LostCounts()
        {
            var context = new AgentContext(null, null, 1);
            var axis = new Consideration { Input = ConsiderationInput.TimeSinceSeen, InputMax = 4f };

            Assert.IsFalse(axis.Evaluate(context, out _), "Never seen: not \"a long time ago\"");

            context.Target = NewTransform("Target", Vector3.right);
            Assert.IsTrue(axis.Evaluate(context, out UtilityScore inSight));
            Assert.AreEqual(0f, (float)inSight, 1e-5f, "In sight now");

            context.Track.Drop();
            context.Advance(2f);
            Assert.AreEqual(0.5f, axis.Evaluate(context), 1e-5f, "2 s of 4");
        }
    }
}
