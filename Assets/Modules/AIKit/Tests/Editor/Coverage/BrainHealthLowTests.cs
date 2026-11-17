using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Health Low's subject (whole-plugin recheck, brain): a new subject is a new event — the Brain's target switched
    /// straight to another low one, or the lowest ally changed from one wounded ally to another — and the allies are
    /// read where their health lives (their own status): the reaction keeps no memory of every ally it ever met, a
    /// think allocates nothing when new allies come, and a destroyed ally is never the one it answers.
    /// </summary>
    public sealed class BrainHealthLowTests
    {
        private const string TARGET = "Target";
        private const float STEP = 0.05f;

        private readonly List<GameObject> _created = new();
        private BrainGraphBuilder _graph;
        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp()
        {
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _entered.Clear();
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
            GraphRig.DestroyAll();
        }

        [Test]
        public void HealthLowReaction_Target_TheBrainSwitchesDirectlyToAnotherLowTarget_AnswersItToo()
        {
            GameObject a = Enemy("A", 20f);
            GameObject b = Enemy("B", 20f);
            GraphRig rig = NewRig(7);
            Decide(new HealthLowReaction
            {
                Whose = HealthSubject.Target, Below = 30f, Slack = 10f, Chance = 1f, ReactionTime = 0f,
                ReactionTimeMax = 0f, Cooldown = 0f
            }, 1, Hold("Finish Off", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run(rig, a.transform);
            Assert.IsTrue(Reaches(rig, "Finish Off"), "A is low: answered");
            Assert.IsTrue(Reaches(rig, "Rest"), "The answer's chain ended");

            rig.Agent.Blackboard.Set(TARGET, b.transform);              // straight to another low one, never none between

            Assert.IsTrue(Reaches(rig, "Finish Off", 0.3f), "A new subject under the line is a new event");
            Assert.AreEqual(2, _entered.Count(entry => entry.Name == "Finish Off"));
        }

        [Test]
        public void HealthLowReaction_Target_ANewTargetUnderTheSlackLineOnly_IsNotLow()
        {
            // The slack is the hysteresis of one subject's health: a new subject is judged by the line itself
            GameObject a = Enemy("A", 20f);
            GameObject b = Enemy("B", 35f);
            GraphRig rig = NewRig(7);
            Decide(new HealthLowReaction
            {
                Whose = HealthSubject.Target, Below = 30f, Slack = 10f, Chance = 1f, ReactionTime = 0f,
                ReactionTimeMax = 0f, Cooldown = 0f
            }, 1, Hold("Finish Off", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run(rig, a.transform);
            Assert.IsTrue(Reaches(rig, "Finish Off"));
            Assert.IsTrue(Reaches(rig, "Rest"));

            rig.Agent.Blackboard.Set(TARGET, b.transform);
            rig.StepSeconds(0.5f, STEP);

            Assert.AreEqual(1, _entered.Count(entry => entry.Name == "Finish Off"), "B at 35%: not low by the line of 30%");
        }

        [Test]
        public void HealthLowReaction_Ally_TheLowestAllyChangesToAnotherWoundedOne_AnswersItToo()
        {
            var world = new AIWorld();
            GraphRig me = NewRig(3, world);
            GraphRig first = Member(world, new Vector3(2f, 0f, 0f), 20f, out FakeHealth firstHealth);
            Member(world, new Vector3(3f, 0f, 0f), 25f, out _);
            var reaction = new HealthLowReaction
            {
                Whose = HealthSubject.Ally, Below = 30f, Slack = 10f, Range = 10f, ReactionTime = 0f,
                ReactionTimeMax = 0f, Cooldown = 0f
            };
            Decide(reaction, 1, Hold("Help", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run(me, null);
            Assert.IsTrue(Reaches(me, "Help"), "An ally at 20%: answered");
            Assert.AreSame(first.Agent, reaction.LowestAllyFor(me.Context));
            Assert.IsTrue(Reaches(me, "Rest"));

            firstHealth.CurrentHealth = 90f;                            // healed: the lowest is now the other, at 25%

            Assert.IsTrue(Reaches(me, "Help", 0.3f), "Another wounded ally is the lowest: a new event");
        }

        [Test]
        public void HealthLowReaction_Ally_AlliesThatAreGone_AreForgotten_NewAlliesAllocateNothing()
        {
            var world = new AIWorld();
            GraphRig me = NewRig(3, world);
            me.Context.ExplainsDecisions = false;
            var allies = new List<GraphRig>();
            for (int i = 0; i < 3; i++)
                allies.Add(Member(world, new Vector3(2f + i, 0f, 0f), 80f, out _));
            var reaction = new HealthLowReaction
            {
                Whose = HealthSubject.Ally, Below = 30f, Range = 10f, ReactionTime = 0f, ReactionTimeMax = 0f
            };
            Decide(reaction, 1, Hold("Help", 0.2f));
            Decide(new PatrolReasoner(), 2, Hold("Rest"));
            Run(me, null, track: false);
            me.StepSeconds(1f, STEP);                                   // warmed up: every list grown

            // Waves: the allies of a wave fall, new ones come (the roster's own list is grown before the thinks are
            // measured — the reaction's thinks are what is measured)
            long allocated = 0;
            var seen = new List<AICharacter>();
            var newcomers = new List<GraphRig>();
            for (int wave = 0; wave < 4; wave++)
            {
                foreach (GraphRig ally in allies)
                    Object.DestroyImmediate(ally.Agent.gameObject);
                allies.Clear();
                for (int i = 0; i < 3; i++)
                    allies.Add(Member(world, new Vector3(2f + i, 0f, 0f), 60f + i, out _));
                foreach (GraphRig ally in allies)
                    ((ICharacterStatus)ally.Agent).TryGetStat(CharacterStat.HealthPercent, out _);  // its own health found
                newcomers.AddRange(allies);

                seen.Clear();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 4; i++)
                {
                    me.Step(STEP);
                    seen.Add(reaction.LowestAllyFor(me.Context));
                }

                allocated += System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsTrue(seen.All(ally => ally == null || allies.Exists(rig => rig.Agent == ally)),
                    $"Wave {wave}: never a destroyed ally");
                Assert.AreSame(allies[0].Agent, seen[seen.Count - 1], $"Wave {wave}: the lowest of the newcomers");
            }

            Assert.AreEqual(0, allocated, "New allies come, wave after wave: a think allocates nothing");
            Assert.AreEqual(0, DestroyedCharactersHeldBy(reaction.Record<AgentTask.Run>(me.Context).Own),
                "The reaction's memory holds none of the allies that are gone (invariant 11)");
        }

        // ---------- the rig ----------

        // How many destroyed characters an object of memory keeps (in a field, a list or a dictionary's keys)
        private static int DestroyedCharactersHeldBy(object memory)
        {
            int held = 0;
            if (memory == null)
                return held;

            const System.Reflection.BindingFlags FLAGS = System.Reflection.BindingFlags.Instance |
                                                         System.Reflection.BindingFlags.Public |
                                                         System.Reflection.BindingFlags.NonPublic;
            foreach (System.Reflection.FieldInfo field in memory.GetType().GetFields(FLAGS))
            {
                object value = field.GetValue(memory);
                if (value is AICharacter single)
                {
                    held += single == null ? 1 : 0;
                    continue;
                }

                System.Collections.IEnumerable items = value is System.Collections.IDictionary dictionary
                    ? dictionary.Keys
                    : value as System.Collections.IEnumerable;
                if (items == null || value is string)
                    continue;

                foreach (object item in items)
                {
                    if (item is AICharacter character && character == null)
                        held++;
                }
            }

            return held;
        }

        private GameObject Enemy(string name, float health)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = new Vector3(2f, 0f, 0f);
            FakeHealth fake = go.AddComponent<FakeHealth>();
            fake.MaxHealth = 100f;
            fake.CurrentHealth = health;
            return go;
        }

        private static GraphRig NewRig(int seed, IAIWorld world = null)
        {
            var rig = new GraphRig(seed, trace: false, world: world) { ThinkInterval = 0f };
            rig.Context.ExplainsDecisions = true;
            return rig;
        }

        // An agent of the world at a place, of faction 0, with a health in percent
        private static GraphRig Member(IAIWorld world, Vector3 at, float health, out FakeHealth fake)
        {
            GraphRig rig = NewRig(1, world);
            rig.Actuator.Position = at;
            rig.Agent.gameObject.SetActive(true);
            fake = rig.Agent.gameObject.AddComponent<FakeHealth>();
            fake.MaxHealth = 100f;
            fake.CurrentHealth = health;
            return rig;
        }

        private void Decide(ReasonerTask decision, int priority, AgentState chain) =>
            _graph.Do(_graph.Decision(decision, priority), chain);

        private AgentState Hold(string name, float seconds = 100f) => _graph.Block(new WaitTask(seconds), name);

        private void Run(GraphRig rig, Transform target, bool track = true)
        {
            rig.SetGraph(_graph.Build());
            rig.Agent.Blackboard.Set(TARGET, target);
            if (!track)
                return;

            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, rig.Context.LocalTime));
            };
        }

        private static bool Reaches(GraphRig rig, string state, float seconds = 2f) =>
            rig.StepUntil(() => rig.StateName == state, seconds, STEP);
    }
}
