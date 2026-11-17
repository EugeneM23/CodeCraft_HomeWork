using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Senses sensor owns the character's senses (step 90, C21: the Senses section of the character is gone): the
    /// senses exist only while a graph with the sensor runs, their state is the sensor's for each agent, what they see
    /// with is the sensor's one setting (a Perception Profile asset, empty — the defaults), a spawner may give one agent a
    /// profile of its own, and the cones open around where a body in 3D looks — not round the world's +X, as the old
    /// 2D-born senses did for every NavMesh body.
    /// </summary>
    public sealed class SensesSensorTests
    {
        private const float DT = 0.05f;
        private const string TARGET = "Target";

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        // A Brain that thinks about what the Senses sensor writes; its one decision only waits
        private static BehaviorGraph Seeing(SensesSensorTask senses)
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            senses.StoreIn = TARGET;
            senses.Interval = 0f;
            g.Sensor(senses);
            g.BrainTask.Target = TARGET;
            g.Chain(g.Always(), new WaitTask(100f));
            return g.Build();
        }

        // A rig whose body has eyes (a scanner of the test's candidates)
        private static GraphRig WithEyes(out GraphRig.RigScanner eyes)
        {
            var rig = new GraphRig(trace: false);
            eyes = new GraphRig.RigScanner();
            rig.Context.Scanner = eyes;
            return rig;
        }

        private static PerceptionProfile EveryStep(System.Action<PerceptionProfile> set = null) =>
            PerceptionProfile.Create(profile =>
            {
                profile.VisionIntervalMin = 0f;
                profile.VisionIntervalMax = 0f;
                set?.Invoke(profile);
            });

        private static Transform Candidate(GraphRig.RigScanner eyes, Vector3 at)
        {
            var go = new GameObject("Candidate") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            eyes.Candidates.Add(go.transform);
            return go.transform;
        }

        [Test]
        public void Senses_NoneInTheGraph_TheCharacterHasNoSenses()
        {
            GraphRig rig = WithEyes(out _);
            rig.SetGraph(GraphRig.OneState(new WaitTask(100f), "Idle"));

            rig.Step(DT, 5);

            Assert.That(rig.Context.Perception, Is.Null, "no Senses sensor: no senses, whatever eyes the body has");
        }

        [Test]
        public void Senses_TheGraphStarts_TheSensorOpensTheSensesWithItsProfile_TheGraphStops_TheyClose()
        {
            GraphRig rig = WithEyes(out _);
            PerceptionProfile profile = EveryStep();
            var senses = new SensesSensorTask { Profile = profile };
            rig.SetGraph(Seeing(senses));

            rig.Step(DT, 2);

            Assert.That(rig.Context.Perception, Is.Not.Null, "the graph started: the senses are open");
            Assert.That(rig.Context.Perception, Is.SameAs(senses.PerceptionOf(rig.Context)), "…the sensor's own, for this agent");
            Assert.That(rig.Context.Perception.Profile, Is.SameAs(profile), "…seeing with its profile");

            rig.SetGraph(null);
            Assert.That(rig.Context.Perception, Is.Null, "the graph stopped: the senses closed with it");
        }

        [Test]
        public void Senses_TwoAgentsOfOneGraph_EachHasSensesOfItsOwn()
        {
            GraphRig first = WithEyes(out _);
            GraphRig second = WithEyes(out _);
            var senses = new SensesSensorTask { Profile = EveryStep() };
            BehaviorGraph graph = Seeing(senses);
            first.SetGraph(graph);
            second.SetGraph(graph);

            first.Step(DT, 2);
            second.Step(DT, 2);

            Assert.That(first.Context.Perception, Is.Not.Null);
            Assert.That(second.Context.Perception, Is.Not.Null);
            Assert.That(first.Context.Perception, Is.Not.SameAs(second.Context.Perception), "the state is per agent");
        }

        [Test]
        public void Senses_NoProfile_SeesWithTheDefaults()
        {
            GraphRig rig = WithEyes(out _);
            rig.SetGraph(Seeing(new SensesSensorTask()));

            rig.Step(DT, 2);

            Assert.That(rig.Context.Perception.Profile, Is.SameAs(PerceptionProfile.Defaults));
        }

        [Test]
        public void Senses_AProfileGivenInCodeForThisAgent_WinsOverTheSensors()
        {
            GraphRig rig = WithEyes(out _);
            PerceptionProfile own = EveryStep(profile => profile.FocusRange = 30f);
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = EveryStep() }));

            SensesSensorTask.SetProfile(rig.Agent, own);
            rig.Step(DT, 2);

            Assert.That(rig.Context.Perception.Profile, Is.SameAs(own), "a spawner's own sight for this one fighter");
        }

        [Test]
        public void Senses_ABodyWithoutEyes_OpensNone_TheTargetHandedOverIsWhatItKnows()
        {
            var rig = new GraphRig(trace: false);                     // no scanner: the game hands the target over
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = EveryStep() }));
            Transform target = rig.CreateTargetAt(new Vector3(3f, 0f, 0f));

            rig.Step(DT, 4);

            Assert.That(rig.Context.Perception, Is.Null, "nothing to see with: no senses opened");
            Assert.That(rig.Context.Blackboard.Get<Transform>(TARGET), Is.SameAs(target), "the target handed over is known");
        }

        [Test]
        public void Senses_SeeATargetInFront_WriteItWhenTheCharacterKnowsIt()
        {
            GraphRig rig = WithEyes(out GraphRig.RigScanner eyes);
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = EveryStep() }));
            Transform foe = Candidate(eyes, new Vector3(5f, 0f, 0f));          // the test body looks along +X

            for (int i = 0; i < 60 && rig.Context.Blackboard.Get<Transform>(TARGET) == null; i++)
                rig.Step(DT);

            Assert.That(rig.Context.Blackboard.Get<Transform>(TARGET), Is.SameAs(foe), "seen, known, written");
            Assert.That(rig.Context.Perception.Level, Is.EqualTo(AlertLevel.Aware));
        }

        [Test]
        public void Senses_ABodyInThreeD_TheConesOpenWhereItLooks_NotRoundTheWorldsX()
        {
            // A NavMesh body looked along the world's +X to its senses wherever it turned (the 2D-born facing). A body in 3D
            // looking along −Z sees what is ahead of it there, and not what lies along +X
            PerceptionProfile narrow = EveryStep(profile =>
            {
                profile.FocusHalfAngle = 45f;
                profile.PeripheryRange = 0f;
                profile.BackRange = 1f;
            });
            GraphRig ahead = WithEyes(out GraphRig.RigScanner aheadEyes);
            GraphRig aside = WithEyes(out GraphRig.RigScanner asideEyes);
            foreach (GraphRig rig in new[] { ahead, aside })
            {
                rig.Actuator.LooksIn3D = true;
                rig.Actuator.Face(Vector3.back);
                rig.SetGraph(Seeing(new SensesSensorTask { Profile = narrow }));
            }

            Candidate(aheadEyes, new Vector3(0f, 0f, -5f));
            Candidate(asideEyes, new Vector3(5f, 0f, 0f));
            ahead.Step(DT, 40);
            aside.Step(DT, 40);

            Assert.That(ahead.Context.Perception.Tracks.Count, Is.EqualTo(1), "ahead of where it looks: seen");
            Assert.That(aside.Context.Perception.Tracks.Count, Is.Zero, "along the world's +X, beside it: not seen");
        }

        [Test]
        public void Senses_TheProfileAsset_IsKeptByTheGraphAsset_AndComesBackOnTheNode()
        {
            PerceptionProfile profile = EveryStep();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                GraphSerializer.Write(Seeing(new SensesSensorTask { Profile = profile }), asset);

                Assert.That(asset.Objects, Has.Member(profile), "the asset keeps the reference");
                SensesSensorTask read = null;
                asset.Compile().VisitNodes(node => read ??= node as SensesSensorTask);
                if (read == null)
                {
                    foreach (AgentTask task in asset.Compile().Root.Tasks)
                        read ??= task as SensesSensorTask;
                }

                Assert.That(read, Is.Not.Null);
                Assert.That(read.Profile, Is.SameAs(profile), "the node sees with it again");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
