using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The character's senses (AgentPerception, the Senses sensor) in the corners the step-90 recheck found: a crowd
    /// bigger than the tracks it keeps (a newcomer waits, the known ones keep their places — handbook §9б.2 p.5), a blow
    /// from somebody unseen (handbook §9.3: damage always wakes); and the rest of the ladder — what the sensor writes at
    /// each level, what it tells its world, its profile, a graph swapped, the lose range behind, the back zone, memory,
    /// a target switched off, Clear, a noise, Alert's limits, the look interval, where a body looks — and a step that
    /// allocates nothing.
    /// </summary>
    public sealed class SensesPerceptionTests
    {
        private const float DT = 0.02f;
        private const string TARGET = "Target";

        private readonly Things _things = new();

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
        }

        // ---------- a crowd ----------

        [Test]
        public void AgentPerception_FiveInView_TheFourKnownKeepTheirPlaces_ANewcomerEvictsNobody()
        {
            PerceptionProfile profile = SeesAllAround();
            GraphRig rig = Perceiving(profile);
            Transform[] faces = Faces(rig, 3f, 4f, 5f, 6f, 7f);

            rig.StepSeconds(0.5f, DT);

            AgentPerception perception = rig.Context.Perception;
            Assert.AreEqual(4, perception.Tracks.Count, "As many tracks as it keeps");
            CollectionAssert.AreEquivalent(faces.Take(4), perception.Tracks.Select(track => track.Transform),
                "The four it saw first keep their places: one more face in a crowd matters less than the ones it knows");
            float full = Mathf.Min(1f, profile.GainPerSecond * 0.5f);
            foreach (PerceivedTarget track in perception.Tracks)
                Assert.AreEqual(full, track.Awareness, 1e-3f, $"{track.Transform.name}: known in full (Gain × 0.5 s)");
        }

        [Test]
        public void AgentPerception_FourFullyKnown_AFifthComesIntoView_TheInterestStaysOnTheNearest()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            Transform[] faces = Faces(rig, 3f, 4f, 5f, 6f);
            rig.StepSeconds(0.5f, DT);
            AgentPerception perception = rig.Context.Perception;
            Assume.That(perception.Best.Transform, Is.SameAs(faces[0]), "Set-up: equally known, the nearest is the interest");

            Transform fifth = Face(rig, 8f);
            for (int i = 0; i < Mathf.CeilToInt(0.2f / DT); i++)
            {
                rig.Step(DT);
                Assert.AreSame(faces[0], perception.Best.Transform, $"Step {i}: the interest stays where it was");
                Assert.AreSame(faces[0], rig.Context.Target, $"Step {i}: so does the target");
            }

            Assert.AreEqual(1f, perception.Tracks.First(track => track.Transform == faces[0]).Awareness, 1e-3f,
                "The nearest is still known in full");
            Assert.IsFalse(perception.Tracks.Any(track => track.Transform == fifth), "The newcomer waits for a free place");
        }

        // ---------- a blow ----------

        [Test]
        public void Senses_ABlowFromBehind_MakesTheAttackerKnownAtOnce()
        {
            var rig = new GraphRig(trace: false);
            var eyes = new GraphRig.RigScanner();
            rig.Context.Scanner = eyes;
            PerceptionProfile profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
            });
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = profile }));
            Transform attacker = _things.Make("Attacker", new Vector3(-10f, 0f, 0f));      // the body looks along +X
            eyes.Candidates.Add(attacker);
            Assume.That(10f, Is.GreaterThan(profile.BackRange), "Behind it and beyond its back range: never seen");

            rig.StepSeconds(1f, DT);
            Assert.AreEqual(AlertLevel.Idle, rig.Context.Perception.Level, "Before the blow: nobody noticed");
            Assert.IsNull(rig.Context.Blackboard.Get<Transform>(TARGET));

            rig.Agent.TakeHit(new HitInfo(attacker, 1f));
            rig.Step(DT);

            Assert.AreEqual(AlertLevel.Aware, rig.Context.Perception.Level, "Damage always wakes (handbook §9.3)");
            Assert.AreEqual(1f, rig.Context.Track.Awareness, 1e-4f, "...known in full at once");
            Assert.AreSame(attacker, rig.Context.Blackboard.Get<Transform>(TARGET), "...and the Senses write it");

            rig.StepSeconds(1f, DT);
            Assume.That(10f, Is.LessThanOrEqualTo(profile.LoseRange));
            Assert.AreSame(attacker, rig.Context.Blackboard.Get<Transform>(TARGET),
                "Known, it is kept up to Lose Range whichever way the body looks");
        }

        // ---------- the Senses sensor ----------

        [Test]
        public void Senses_WritesTheTargetOnlyWhileAware_NotWhileSuspiciousOrSearching()
        {
            GraphRig rig = WithEyes(out GraphRig.RigScanner eyes);
            var sight = new SwitchSight();
            rig.Context.Sight = sight;
            PerceptionProfile profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 180f;
                p.GainPerSecond = 2f;
                p.NoticeThreshold = 0.3f;
                p.AwareThreshold = 0.8f;
            });
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = profile }));
            Transform foe = _things.Make("Foe", new Vector3(5f, 0f, 0f));
            eyes.Candidates.Add(foe);

            rig.StepSeconds(0.2f, DT);
            Assert.AreEqual(AlertLevel.Suspicious, rig.Context.Perception.Level, "0.4 of awareness: it noticed something");
            Assert.IsNull(rig.Context.Blackboard.Get<Transform>(TARGET), "Suspicious only: nothing written yet");

            rig.StepSeconds(0.3f, DT);
            Assert.AreEqual(AlertLevel.Aware, rig.Context.Perception.Level, "0.8 by 0.4 s: it knows");
            Assert.AreSame(foe, rig.Context.Blackboard.Get<Transform>(TARGET), "Known: written");

            sight.Clear = false;
            rig.Step(DT);
            Assert.AreEqual(AlertLevel.Searching, rig.Context.Perception.Level, "Lost sight of what it knew: it searches");
            Assert.IsNull(rig.Context.Blackboard.Get<Transform>(TARGET), "Searching: nothing written — only a place is known");
        }

        [Test]
        public void Senses_ReportsItsAlertLevelToItsWorld()
        {
            var world = new AIWorld();
            GraphRig rig = WithEyes(out GraphRig.RigScanner eyes);
            rig.Agent.SetWorld(world);
            PerceptionProfile profile = SeesAllAround();
            profile.DecayPerSecond = 1f;
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = profile }));
            Transform foe = _things.Make("Foe", new Vector3(5f, 0f, 0f));
            eyes.Candidates.Add(foe);

            // The senses open with the graph, on its first step
            Assert.IsTrue(StepUntil(rig, world, () => rig.Context.Perception?.Level == AlertLevel.Aware, 2f), "Set-up: it knows");
            world.BeginStep(DT);
            Assert.AreEqual(AlertLevel.Aware, world.Alert, "The world hears how alert its agents are");

            eyes.Candidates.Remove(foe);
            Assert.IsTrue(StepUntil(rig, world, () => rig.Context.Perception?.Level == AlertLevel.Idle, profile.MemoryTime + 3f),
                "Forgotten after its memory");
            rig.Step(DT);
            world.BeginStep(DT);
            Assert.AreEqual(AlertLevel.Idle, world.Alert, "...and calm again");
        }

        [Test]
        public void Senses_SetProfileNull_TheSensorsOwnProfileAgain()
        {
            GraphRig rig = WithEyes(out _);
            PerceptionProfile own = SeesAllAround();
            PerceptionProfile code = SeesAllAround();
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = own }));
            rig.Step(DT);

            SensesSensorTask.SetProfile(rig.Agent, code);
            rig.Step(DT);
            Assert.AreSame(code, rig.Context.Perception.Profile, "A spawner's own sight for this one");

            SensesSensorTask.SetProfile(rig.Agent, null);
            rig.Step(DT);
            Assert.AreSame(own, rig.Context.Perception.Profile, "Null: the sensor's own asset again");
        }

        [Test]
        public void Senses_GraphSwapped_TheNewSensorsSensesOpen_TheOldOnesAreGone()
        {
            GraphRig rig = WithEyes(out _);
            PerceptionProfile first = SeesAllAround();
            PerceptionProfile second = SeesAllAround();
            var sensorA = new SensesSensorTask { Profile = first };
            var sensorB = new SensesSensorTask { Profile = second };
            rig.SetGraph(Seeing(sensorA));
            rig.Step(DT, 2);
            AgentPerception before = rig.Context.Perception;
            Assume.That(before, Is.SameAs(sensorA.PerceptionOf(rig.Context)));

            rig.SetGraph(Seeing(sensorB));
            rig.Step(DT, 2);

            Assert.IsNotNull(rig.Context.Perception);
            Assert.AreSame(sensorB.PerceptionOf(rig.Context), rig.Context.Perception, "The new graph's sensor opened its own");
            Assert.AreNotSame(before, rig.Context.Perception, "...not the old graph's");
            Assert.AreSame(second, rig.Context.Perception.Profile);
            Assert.IsNull(sensorA.PerceptionOf(rig.Context), "The old sensor's memory of this agent is gone with its graph");
        }

        // ---------- the perception ----------

        [Test]
        public void AgentPerception_HeldTargetBehind_KeptUpToLoseRange_LostBeyond()
        {
            PerceptionProfile profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 45f;
                p.PeripheryRange = 0f;
                p.BackRange = 1f;
                p.LoseRange = 16f;
            });
            GraphRig rig = Perceiving(profile);                      // the test body looks along +X
            Transform foe = Face(rig, 5f);
            Assert.IsTrue(rig.StepUntil(() => rig.Context.Perception.Level == AlertLevel.Aware, 2f, DT), "Set-up: known");
            AgentPerception perception = rig.Context.Perception;

            foe.position = new Vector3(-10f, 0f, 0f);
            rig.Step(DT);
            Assume.That(Vector3.Distance(foe.position, rig.Context.Position + Vector3.up * profile.EyeHeight),
                Is.LessThanOrEqualTo(profile.LoseRange));
            Assert.IsTrue(perception.IsVisible(foe), "Known: kept behind it, up to Lose Range");
            Assert.AreEqual(AlertLevel.Aware, perception.Level);

            foe.position = new Vector3(-17f, 0f, 0f);
            rig.Step(DT);
            Assert.IsFalse(perception.IsVisible(foe), "Beyond Lose Range: lost");
            Assert.AreEqual(AlertLevel.Searching, perception.Level);
        }

        [Test]
        public void AgentPerception_BackZone_NoticesAtAQuarterOfTheFocusRate()
        {
            PerceptionProfile profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusRange = 12f;
                p.FocusHalfAngle = 45f;
                p.PeripheryRange = 8f;
                p.PeripheryHalfAngle = 100f;
                p.BackRange = 3f;
                p.GainPerSecond = 2f;
            });
            GraphRig ahead = Perceiving(profile);
            GraphRig behind = Perceiving(profile);
            Face(ahead, 5f);
            Face(behind, -2f);                                      // eye distance 2.5 ≤ 3, 143° off the facing

            int aheadSteps = StepsUntilSuspicious(ahead);
            int behindSteps = StepsUntilSuspicious(behind);

            int expectedAhead = Mathf.CeilToInt(profile.NoticeThreshold / (profile.GainPerSecond * DT) - 1e-3f);
            int expectedBehind = Mathf.CeilToInt(profile.NoticeThreshold / (profile.GainPerSecond * 0.25f * DT) - 1e-3f);
            Assert.That(aheadSteps, Is.InRange(expectedAhead - 1, expectedAhead + 1), "In the focus: the full rate");
            Assert.That(behindSteps, Is.InRange(expectedBehind - 1, expectedBehind + 1), "Behind: a quarter of it");
        }

        [Test]
        public void AgentPerception_LostTarget_ForgottenOnlyAfterMemoryTime()
        {
            PerceptionProfile profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 180f;
                p.DecayPerSecond = 0.5f;
                p.MemoryTime = 4f;
            });
            GraphRig rig = Perceiving(profile);
            var sight = new SwitchSight();
            rig.Context.Sight = sight;
            Face(rig, 5f);
            rig.StepSeconds(0.6f, DT);
            AgentPerception perception = rig.Context.Perception;
            Assume.That(perception.Awareness, Is.EqualTo(1f).Within(1e-4f), "Set-up: known in full");

            sight.Clear = false;
            rig.StepSeconds(3.9f, DT);
            Assert.AreEqual(1, perception.Tracks.Count, "Still remembered");
            Assert.AreEqual(0f, perception.Tracks[0].Awareness, 1e-4f, "Awareness gone at 2 s (1 / 0.5 per second)");
            Assert.AreEqual(AlertLevel.Searching, perception.Level, "...the place still worth searching");

            rig.StepSeconds(0.2f, DT);
            Assert.AreEqual(0, perception.Tracks.Count, "After Memory Time: forgotten");
            Assert.AreEqual(AlertLevel.Idle, perception.Level);
        }

        [Test]
        public void AgentPerception_TrackedTargetSwitchedOff_IsNeverSeenAgain()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            Transform foe = Face(rig, 5f);
            Assume.That(rig.StepUntil(() => rig.Context.Perception.Level == AlertLevel.Aware, 2f, DT), Is.True);

            foe.gameObject.SetActive(false);                        // the scanner still lists it
            rig.Step(DT);

            Assert.IsFalse(rig.Context.Perception.IsVisible(foe), "Switched off (pooled, dead): never seen");
            Assert.AreEqual(AlertLevel.Searching, rig.Context.Perception.Level);
            Assert.IsFalse(rig.Context.HasTarget, "...and no target any more");
        }

        [Test]
        public void AgentPerception_Clear_ForgetsTracksLevelAndMemory()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            Face(rig, 5f);
            Assume.That(rig.StepUntil(() => rig.Context.Perception.Level == AlertLevel.Aware, 2f, DT), Is.True);
            AgentPerception perception = rig.Context.Perception;

            perception.Clear();

            Assert.AreEqual(0, perception.Tracks.Count);
            Assert.IsNull(perception.Best);
            Assert.AreEqual(AlertLevel.Idle, perception.Level);
            Assert.IsFalse(rig.Context.Track.Remembers, "No place to search either");
            Assert.IsFalse(rig.Context.HasTarget);
        }

        [Test]
        public void AgentPerception_HearANoiseWithoutSubject_NoTrack_ThinksOnTheNextStep()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            rig.ThinkInterval = 10f;
            rig.SetGraph(GraphRig.OneState(new WaitTask(100f), "Idle"));
            rig.Step(DT);
            int decisions = rig.Agent.DecisionCount;

            rig.Context.Perception.Hear(new Signal("Noise", new Vector3(3f, 0f, 0f)));
            rig.Step(DT);

            Assert.AreEqual(0, rig.Context.Perception.Tracks.Count, "A noise is about nobody: no track");
            Assert.AreEqual(AlertLevel.Idle, rig.Context.Perception.Level);
            Assert.AreEqual(decisions + 1, rig.Agent.DecisionCount, "It thinks on the next step, not after its 10 s");
        }

        [Test]
        public void AgentPerception_AlertAboveOne_ClampedToOne_SameFaction_Ignored()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            AgentPerception perception = rig.Context.Perception;
            Transform foe = _things.Make("Foe", new Vector3(4f, 0f, 0f));

            perception.Alert(foe, 3f);
            Assert.AreEqual(1f, perception.Tracks[0].Awareness, "Awareness is at most 1");

            rig.Agent.Faction = 2;
            Transform ally = _things.Make("Ally", new Vector3(-4f, 0f, 0f));
            ally.gameObject.AddComponent<FactionMark>().Side = 2;
            perception.Alert(ally);
            Assert.IsFalse(perception.Tracks.Any(track => track.Transform == ally), "Its own side is nobody's target");
        }

        [Test]
        public void AgentPerception_VisionIntervalMinAboveMax_LooksEveryMax()
        {
            const float MAX = 0.2f;
            GraphRig rig = Perceiving(PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0.5f;
                p.VisionIntervalMax = MAX;
            }));
            AgentPerception perception = rig.Context.Perception;
            for (int i = 0; i < 100 && perception.Looks == 0; i++)
                perception.Step(DT);
            int first = perception.Looks;

            for (int i = 0; i < Mathf.RoundToInt(2f / DT); i++)
                perception.Step(DT);

            Assert.That(perception.Looks - first, Is.InRange(Mathf.RoundToInt(2f / MAX) - 1, Mathf.RoundToInt(2f / MAX) + 1),
                "A minimum above the maximum: every maximum");
        }

        [Test]
        public void AgentPerception_LookOf_3DForwardFlattened_SideViewFacing_OrNothing()
        {
            Assert.AreEqual(Vector3.right, AgentPerception.LookOf(new AgentBodyInfo { Forward = new Vector3(1f, 1f, 0f) }, Vector3.up),
                "In 3D: its forward over the ground");
            Assert.AreEqual(Vector3.left, AgentPerception.LookOf(new AgentBodyInfo { Facing = -1 }, Vector3.up),
                "Side view: along the right of its up, facing left");
            Assert.AreEqual(Vector3.zero, AgentPerception.LookOf(new AgentBodyInfo { Facing = 0 }, Vector3.up),
                "No facing: nowhere in particular");
            Assert.AreEqual(Vector3.down, AgentPerception.LookOf(new AgentBodyInfo { Facing = 1 }, Vector3.right),
                "Side view on a wall (up along +X): its right is −Y");
        }

        // ---------- no allocation (Unity's own GC.Alloc counter: GC.GetAllocatedBytesForCurrentThread says 0 in the editor) ----------

        [Test]
        public void AgentPerception_Steps_AllocateNothing_ByUnitysCounter()
        {
            GraphRig rig = Perceiving(SeesAllAround());
            rig.Context.Sight = new SwitchSight();
            Faces(rig, 3f, 4f, 5f, 6f, 7f);                         // a crowd: one more than it keeps
            rig.Step(DT, 20);                                       // warm up: tracks and lists exist

            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                    rig.Context.Perception.Step(DT);
            }, Is.Not.AllocatingGCMemory(), "No allocation in the perception's step");
        }

        [Test]
        public void Senses_AGraphThatSeesWithThem_StepsWithoutAllocating_ByUnitysCounter()
        {
            var world = new AIWorld();
            GraphRig rig = WithEyes(out GraphRig.RigScanner eyes);
            rig.Agent.SetWorld(world);
            var sight = new SwitchSight();
            rig.Context.Sight = sight;
            rig.SetGraph(Seeing(new SensesSensorTask { Profile = SeesAllAround() }));
            Transform foe = _things.Make("Foe", new Vector3(5f, 0f, 0f));
            eyes.Candidates.Add(foe);
            for (int i = 0; i < 100; i++)
            {
                sight.Clear = i % 30 < 20;                          // seen, lost, seen again
                world.BeginStep(DT);
                rig.Step(DT);
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    sight.Clear = i % 30 < 20;
                    world.BeginStep(DT);
                    rig.Step(DT);
                }
            }, Is.Not.AllocatingGCMemory(),
                "A step of a character that sees with Senses, finds, loses and finds again, allocates nothing");
        }

        // ---------- helpers ----------

        private static GraphRig WithEyes(out GraphRig.RigScanner eyes)
        {
            var rig = new GraphRig(trace: false);
            eyes = new GraphRig.RigScanner();
            rig.Context.Scanner = eyes;
            return rig;
        }

        private static bool StepUntil(GraphRig rig, AIWorld world, System.Func<bool> condition, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                if (condition())
                    return true;

                world.BeginStep(DT);
                rig.Step(DT);
            }

            return condition();
        }

        private static int StepsUntilSuspicious(GraphRig rig)
        {
            for (int i = 1; i <= 200; i++)
            {
                rig.Step(DT);
                if (rig.Context.Perception.Level == AlertLevel.Suspicious)
                    return i;
            }

            return -1;
        }

        // Looks every step, all round, forgets nothing: only the crowd limit decides who is tracked
        private static PerceptionProfile SeesAllAround() =>
            PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 180f;
                p.DecayPerSecond = 0f;
            });

        private static GraphRig Perceiving(PerceptionProfile profile)
        {
            var rig = new GraphRig(trace: false);
            rig.GivePerception(profile);
            return rig;
        }

        private Transform Face(GraphRig rig, float x)
        {
            Transform face = _things.Make($"Face at {x}", new Vector3(x, 0f, 0f));
            rig.Scanner.Candidates.Add(face);
            return face;
        }

        private Transform[] Faces(GraphRig rig, params float[] xs)
        {
            var faces = new List<Transform>();
            foreach (float x in xs)
                faces.Add(Face(rig, x));
            return faces.ToArray();
        }

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
    }
}
