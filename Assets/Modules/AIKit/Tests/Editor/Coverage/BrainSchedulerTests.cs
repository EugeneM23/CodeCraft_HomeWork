using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Who thinks when (whole-plugin recheck, brain; handbook §27.1 — the agents not reached go first on the next step;
    /// §27.3 — the first decisions spread over the think interval; §29 — one seed, one trace): under a cap no agent
    /// starves; agents that drive themselves in a scene join the scene's world before their first think, so their first
    /// decisions are spread and their random comes from the world's seed.
    /// </summary>
    public sealed class BrainSchedulerTests
    {
        private const float DT = 0.02f;

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            DestroySceneWorlds();
            GraphRig.DestroyAll();
        }

        [Test]
        public void AgentScheduler_MoreDueAgentsThanTheCap_EveryAgentGetsItsTurn()
        {
            const int AGENTS = 12, STEPS = 300;
            var world = new AIWorld { Scheduler = { MaxDecisionsPerStep = 1 } };
            var rigs = new List<GraphRig>();
            for (int i = 0; i < AGENTS; i++)
            {
                var rig = new GraphRig(trace: false);
                rig.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning()));
                rig.CreateTargetAt(new Vector3(3f, 0f, 0f));
                rig.Agent.SetWorld(world);              // after the graph: SetGraph resets the first decision
                rigs.Add(rig);
            }

            for (int step = 0; step < STEPS; step++)
            {
                world.BeginStep(DT);
                foreach (GraphRig rig in rigs)
                    rig.Step(DT);
            }

            int[] counts = rigs.Select(rig => rig.Agent.DecisionCount).ToArray();
            string all = string.Join(", ", counts);
            Assert.AreEqual(STEPS, counts.Sum(), $"One decision a step, every step: {all}");
            Assert.GreaterOrEqual(counts.Min(), 20, $"Nobody starves (a fair share is {STEPS / AGENTS}): {all}");
            Assert.LessOrEqual(counts.Max() - counts.Min(), 2, $"Turn by turn: {all}");
        }

        [Test]
        public void AgentScheduler_AnAgentPostponedAndThenPaused_DoesNotHoldTheOthersBack()
        {
            var world = new AIWorld { Scheduler = { MaxDecisionsPerStep = 1 } };
            GraphRig first = Waiting(world);
            GraphRig paused = Waiting(world);
            GraphRig third = Waiting(world);
            foreach (GraphRig rig in new[] { first, paused, third })
                rig.ThinkInterval = 1f;

            world.BeginStep(DT);
            first.Step(DT);
            paused.Step(DT);
            third.Step(DT);
            paused.Agent.IsThinking = false;            // postponed, then paused: it never asks again

            for (int i = 0; i < 6; i++)
            {
                world.BeginStep(DT);
                first.Step(DT);
                paused.Step(DT);
                third.Step(DT);
            }

            Assert.Greater(third.Agent.DecisionCount, 0, "The place kept for an agent that stopped asking is given on");
        }

        [Test]
        public void AgentScheduler_AgentsJoiningTheScenesWorldWhenTheirBrainStarts_FirstDecisionsAreSpread()
        {
            AIWorld world = FreshSceneWorld(1);
            var rigs = new List<GraphRig>();
            for (int i = 0; i < 8; i++)
            {
                GraphRig rig = SceneAgent(seed: i + 1);
                rig.SetGraph(GraphRig.OneState(new WaitTask(100f), "Wait"));
                rigs.Add(rig);
            }

            var deciding = new List<int>();
            int[] before = new int[rigs.Count];
            for (int step = 0; step < 30; step++)
            {
                world.BeginStep(Time.fixedDeltaTime);
                int count = 0;
                for (int i = 0; i < rigs.Count; i++)
                {
                    SelfStep(rigs[i]);
                    if (rigs[i].Agent.DecisionCount != before[i])
                        count++;
                    before[i] = rigs[i].Agent.DecisionCount;
                }

                deciding.Add(count);
            }

            Assert.IsTrue(rigs.All(rig => rig.Agent.World == world), "Each joined the scene's world");
            Assert.LessOrEqual(deciding.Max(), 2,
                $"The first decisions spread over the think interval: deciding per step {string.Join(",", deciding)}");
            Assert.IsTrue(rigs.All(rig => rig.Agent.DecisionCount > 0));
        }

        [Test]
        public void AICharacter_SeedZero_JoiningTheScenesWorldBeforeItsFirstThink_TheSensorsLookTimesReplay()
        {
            float first = DetectedAt();
            float second = DetectedAt();

            Assert.That(first, Is.InRange(0.5f, 1.2f + 2f * DT), "Seen at a look 0.8–1.2 s after the last");
            Assert.AreEqual(first, second, 1e-5f, "One world seed, one trace (handbook §29)");
        }

        // Steps a scene agent of seed 0 that drives itself, a target appearing at 0.5 s: when it was seen
        private float DetectedAt()
        {
            DestroySceneWorlds();
            FreshSceneWorld(42);
            GraphRig rig = SceneAgent(seed: 0);
            var finder = new ListFinder();
            rig.Context.Objects = finder;
            var g = new BrainGraphBuilder();
            g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 10f, LoseRadius = 10f })
            {
                Interval = 1f, StoreIn = "Target"
            });
            g.Variable("Target", VariableType.Transform);
            g.BrainTask.Target = "Target";
            g.Do(g.Decision(new WatchReasoner(), 1), g.Block(new WaitTask(100f), "Watch Block"));
            g.Do(g.Always(), g.Block(new WaitTask(100f), "Rest"));
            rig.SetGraph(g.Build());
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(target);
            target.transform.position = new Vector3(3f, 0f, 0f);

            for (int i = 0; i < 300; i++)
            {
                if (rig.Context.LocalTime >= 0.5f - 1e-4f && finder.Objects.Count == 0)
                    finder.Objects.Add(target.transform);

                SelfStep(rig);
                if (rig.Context.Blackboard.Get<Transform>("Target") != null)
                    return rig.Context.LocalTime;
            }

            return float.MaxValue;
        }

        // ---------- the rigs ----------

        // An agent that drives itself in a scene, no world given
        private static GraphRig SceneAgent(int seed)
        {
            var rig = new GraphRig(seed, trace: false);
            rig.Agent.Simulation = AICharacter.SimulationMode.FixedUpdate;
            return rig;
        }

        private static readonly System.Reflection.MethodInfo FixedUpdate = typeof(AICharacter).GetMethod("FixedUpdate",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // One step as the engine gives it to a character that drives itself in Play (Edit Mode sends it no FixedUpdate)
        private static void SelfStep(GraphRig rig)
        {
            Assert.IsNotNull(FixedUpdate, "AICharacter steps itself in FixedUpdate");
            FixedUpdate.Invoke(rig.Agent, null);
        }

        [Test]
        public void AICharacter_SteppedByItsOwner_JoinsTheScenesWorldOnlyWhenItsBrainStarts_ItsRhythmUntouched()
        {
            // A character stepped by hand (a game's loop, a test) is the owner's: it joins nothing before its first think,
            // and its first decision comes on its first step whatever the scene's world has seen
            DestroySceneWorlds();
            FreshSceneWorld(1);
            GraphRig rig = SceneAgent(seed: 1);
            rig.SetGraph(GraphRig.OneState(new WaitTask(100f), "Wait"));

            rig.Step(DT);

            Assert.AreEqual(1, rig.Agent.DecisionCount, "It thought on its first step");
            Assert.IsNotNull(rig.Agent.World, "Its Brain started: it is in the scene's world from now on");
        }

        private static GraphRig Waiting(IAIWorld world)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.SetGraph(GraphRig.OneState(new WaitTask(100f), "Wait"));
            return rig;
        }

        // The scene's world, made anew (no agent registered with it yet), with this seed
        private AIWorld FreshSceneWorld(int seed)
        {
            DestroySceneWorlds();
            var go = new GameObject("[AI World]");
            _created.Add(go);
            var world = (AIWorld)go.AddComponent<AIWorldRunner>().World;
            world.Seed = seed;
            return world;
        }

        private static void DestroySceneWorlds()
        {
            foreach (AIWorldRunner runner in Object.FindObjectsByType<AIWorldRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(runner.gameObject);
        }

        private sealed class ListFinder : IObjectFinder
        {
            public readonly List<Transform> Objects = new();

            public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
            {
                var found = new List<Transform>();
                FindAll(kind, center, radius, self, found);
                return found.Count > 0 ? found[0] : null;
            }

            public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
            {
                results.Clear();
                foreach (Transform candidate in Objects)
                {
                    if (candidate != null && (candidate.position - center).sqrMagnitude <= radius * radius)
                        results.Add(candidate);
                }
            }
        }
    }
}
