using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The parts a graph is made with (whole-plugin recheck, data): the builder's priorities and arrows; what a node is
    /// called (never its class name, never a label passed off as its name); a node's data under an old field name and
    /// its link fields; and the presets as graphs — the melee chaser's visible ask for a place, every preset checked by
    /// the validator, a tuning copied for one agent.
    /// </summary>
    public sealed class DataGraphPartsTests
    {
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

        // ---------- the builder ----------

        [Test]
        public void BrainGraphBuilder_Decision_PriorityRunsPastTheHighestGiven_AlwaysIsLast()
        {
            var g = new BrainGraphBuilder();
            AgentState watch = g.Decision(new WatchReasoner(), 5);
            AgentState chase = g.Decision(new ChaseReasoner());
            AgentState always = g.Always();

            Assert.AreEqual(5, watch.Reasoner.Priority);
            Assert.AreEqual(6, chase.Reasoner.Priority);
            Assert.AreEqual(7, always.Reasoner.Priority);
            CollectionAssert.AreEqual(new[] { watch, chase, always }, g.Brain.Transitions.Select(wire => wire.Target));
            Assert.IsTrue(g.Brain.Transitions.All(wire => wire.Trigger == TransitionTrigger.Always));

            g.Decision(new PatrolReasoner(), 2);
            Assert.AreEqual(8, g.Decision(new WatchReasoner()).Reasoner.Priority, "A lower number given later moves nothing back");
        }

        [Test]
        public void BrainGraphBuilder_Arrows_DoThenOnFailOnEndOutOnTick_TheirTriggersAndOutputs()
        {
            var g = new BrainGraphBuilder();
            AgentState decision = g.Always();
            AgentState[] chain = g.Chain(decision, new WaitTask(1f), new WaitTask(2f), new WaitTask(3f));
            AgentState x = g.Block(new WaitTask(1f), "X");
            AgentState y = g.Block(new WaitTask(1f), "Y");
            AgentState r = g.Block(new PickRandomTask(), "R");
            AgentState z = g.Block(new WaitTask(1f), "Z");
            AgentState t = g.Block(new SetNumberTask(), "T");
            g.OnFail(chain[0], x);
            g.OnEnd(chain[1], y);
            g.Out(r, 1, z);
            g.OnTick(decision, t);
            g.Sensor(null);
            var radius = new RadiusSensorTask { IsBackground = false };
            g.Sensor(radius);

            Assert.AreEqual(3, chain.Length);
            Assert.IsTrue(chain.All(node => node.Name == "Wait"), "Named by their type");
            Assert.AreEqual(TransitionTrigger.Always, Arrow(decision, chain[0]).Trigger);
            Assert.AreEqual(-1, Arrow(decision, chain[0]).Output);
            Assert.AreEqual(TransitionTrigger.TasksSucceeded, Arrow(chain[0], chain[1]).Trigger);
            Assert.AreEqual(TransitionTrigger.TasksSucceeded, Arrow(chain[1], chain[2]).Trigger);
            Assert.AreEqual(TransitionTrigger.TasksFailed, Arrow(chain[0], x).Trigger);
            Assert.AreEqual(TransitionTrigger.TasksFinished, Arrow(chain[1], y).Trigger);
            Assert.AreEqual(TransitionTrigger.TasksSucceeded, Arrow(r, z).Trigger);
            Assert.AreEqual(1, Arrow(r, z).Output);
            Assert.AreEqual(TransitionTrigger.Tick, Arrow(decision, t).Trigger);
            Assert.AreEqual(1, g.Root.Tasks.Count, "A null sensor is no sensor");
            Assert.IsTrue(g.Root.Tasks[0].IsBackground, "A sensor works beside whatever runs");
        }

        // ---------- names ----------

        [Test]
        public void NodeTitles_Plain_AClassOutsideTheRegistry_InWords()
        {
            Assert.AreEqual("Hold", NodeTitles.Of(new HoldTask()));
            Assert.AreEqual("Flag", NodeTitles.Of(new FlagReasoner()));
            Assert.AreEqual("My", NodeTitles.Of(new MySensorTask()));
            Assert.AreEqual("ABC", NodeTitles.Of(new ABCValue()));
            Assert.AreEqual("Keep Formation", NodeTitles.Of(new KeepFormationTask()));
        }

        [Test]
        public void NodeTitles_NameByType_AStateOfOnlyABackgroundTask_ItsLabelDoesNotBecomeItsName()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new WaitTask(1f)));
            var eyes = new AgentState { Label = "Eyes", Name = "" };
            eyes.AddTask(new HoldTask { IsBackground = true });
            g.Root.AddChild(eyes);
            BehaviorGraph graph = g.Build();

            NodeTitles.NameByType(graph);
            Assert.AreNotEqual("Eyes", eyes.Name, "A label is the user's: it is never copied into the name");

            eyes.Label = "";
            Assert.AreNotEqual("Eyes", eyes.DisplayName, "The label gone, the node is not called by it");
        }

        // ---------- the fields of a node ----------

        [Test]
        public void GraphFields_RenameOldKeys_ANestedKeyOfTheOldName_Stays_BothKeysPresent_NotRenamed()
        {
            const string OLD = "{\"Weight\":0.7,\"Outputs\":[{\"Weight\":3.0,\"NotTwiceInARow\":false}]}";
            var renamed = new RenamedNode();
            JsonUtility.FromJsonOverwrite(GraphFields.RenameOldKeys(typeof(RenamedNode), OLD), renamed);

            Assert.AreEqual(0.7f, renamed.Chance, 1e-5f, "The node's own old key is read under its new name");
            Assert.AreEqual(1, renamed.Outputs.Count);
            Assert.AreEqual(3f, renamed.Outputs[0].Weight, 1e-5f, "A key of the old name inside a nested object is not the node's");

            const string BOTH = "{\"Weight\":0.7,\"Chance\":0.2}";
            Assert.AreEqual(BOTH, GraphFields.RenameOldKeys(typeof(RenamedNode), BOTH), "Both keys there: nothing renamed");
        }

        [Test]
        public void GraphFields_LinksOf_PrivateSerializeFieldIsALink_ReadonlyIsNot_BaseFieldsFirst()
        {
            GraphFields.Link[] links = GraphFields.LinksOf(typeof(LinkedChild));

            CollectionAssert.AreEqual(new[] { "_inner", "Gate", "Values", "Extra" }, links.Select(link => link.Name));
            GraphFields.Link values = links.Single(link => link.Name == "Values");
            Assert.IsTrue(values.IsList);
            Assert.AreEqual(typeof(AgentValue), values.ElementType);
            Assert.IsFalse(links.Single(link => link.Name == "Gate").IsObject);
            Assert.AreEqual("Gate", GraphFields.Find(typeof(LinkedChild), "OldGate")?.Name, "An old name finds the field");
        }

        // ---------- the presets ----------

        [Test]
        public void BehaviorPresets_MeleeChaserWithATokenPool_TheGraphIsTheVisibleAsk()
        {
            var tuning = new BehaviorPresets.Tuning { TokenPool = "Melee" };
            tuning.Abilities.Add(GraphRig.Named("Swing"));
            BehaviorGraph graph = BehaviorPresets.MeleeChaser(tuning);
            List<AgentState> decisions = graph.Brain.Transitions.Select(wire => wire.Target)
                .OrderBy(decision => decision.Reasoner.Priority).ToList();

            Assert.AreEqual(BehaviorPresets.TARGET, CombatBrainTask.Of(graph.Brain).Target);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, decisions.Select(decision => decision.Reasoner.Priority));

            Assert.IsInstanceOf<UseAnyAbilityReasoner>(decisions[0].Reasoner);
            AssertSlot(decisions[0].Reasoner.Condition, AttackSlotIs.Mine, "Melee");
            Assert.IsInstanceOf<UseAbilityTask>(Do(decisions[0]).Tasks[0]);
            Assert.AreEqual(tuning.Recovery, ((WaitTask)Next(Do(decisions[0])).Tasks[0]).Seconds.Get(null), 1e-5f);

            Assert.IsInstanceOf<ChaseReasoner>(decisions[1].Reasoner);
            AssertSlot(decisions[1].Reasoner.Condition, AttackSlotIs.Mine, "Melee");
            Assert.AreEqual(tuning.PreferredRange, ((ChaseTask)Do(decisions[1]).Tasks[0]).StopDistance.Get(null), 1e-5f);

            Assert.IsInstanceOf<CircleReasoner>(decisions[2].Reasoner);
            AssertSlot(decisions[2].Reasoner.Condition, AttackSlotIs.NotMine, "Melee");
            var circle = (CircleTargetTask)Do(decisions[2]).Tasks[0];
            Assert.AreEqual(tuning.KeepMax, circle.Radius.Get(null), 1e-5f);
            Assert.IsFalse(circle.WaitForSlot, "The ask is a block of its own, on the On Tick pin");
            AgentState ask = decisions[2].Transitions.Single(arrow => arrow.Trigger == TransitionTrigger.Tick).Target;
            var take = (TakeAttackSlotTask)ask.Tasks[0];
            Assert.AreEqual("Melee", take.Pool);
            Assert.IsTrue(take.IsInstant, "Wait Time 0: an instant ask, so the On Tick runs it");

            Assert.IsInstanceOf<PatrolReasoner>(decisions[3].Reasoner);
            var patrol = (PatrolTask)Do(decisions[3]).Tasks[0];
            Assert.AreEqual(tuning.PauseAtPoint, patrol.PauseAtPoint.Get(null), 1e-5f);
            Assert.AreEqual(tuning.PauseAtPoint * 2f, patrol.PauseAtPointMax.Get(null), 1e-5f);

            var issues = new List<GraphIssue>();
            ConditionChecks.Check(graph, issues);
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void BehaviorPresets_EveryPreset_WrittenAndValidated_NoErrors_AnEmptySetIsSaid()
        {
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
            {
                List<GraphIssue> empty = Validate(BehaviorPresets.Build(preset, new BehaviorPresets.Tuning()));
                Assert.IsFalse(empty.Exists(issue => issue.IsError), $"{preset}: {string.Join("\n", empty)}");
                Assert.AreEqual(1, empty.Count(issue => issue.Code == GraphIssueCode.EmptyAbilitySet),
                    $"{preset}: its Use Any Ability with no abilities is said");

                var tuning = new BehaviorPresets.Tuning();
                tuning.Abilities.Add(GraphRig.Named("Swing"));
                List<GraphIssue> armed = Validate(BehaviorPresets.Build(preset, tuning));
                Assert.IsFalse(armed.Exists(issue => issue.IsError), $"{preset}: {string.Join("\n", armed)}");
                Assert.IsFalse(armed.Exists(issue => issue.Code == GraphIssueCode.EmptyAbilitySet), preset.ToString());
            }
        }

        [Test]
        public void BehaviorPresets_TuningClone_RouteAndAbilitiesAreCopies_BuildDefaults()
        {
            AbilityAsset swing = GraphRig.Named("Swing");
            var tuning = new BehaviorPresets.Tuning { PatrolPoints = new[] { new Vector3(1f, 0f, 0f) } };
            tuning.Abilities.Add(swing);

            BehaviorPresets.Tuning copy = tuning.Clone();
            copy.PatrolPoints[0] = new Vector3(9f, 0f, 9f);
            copy.Abilities.Clear();

            Assert.AreEqual(new Vector3(1f, 0f, 0f), tuning.PatrolPoints[0], "The route is the copy's own");
            Assert.AreEqual(1, tuning.Abilities.Count, "The abilities' list too");

            BehaviorGraph fallback = null;
            Assert.DoesNotThrow(() => fallback = BehaviorPresets.Build((BehaviorPresets.Preset)99, null));
            CollectionAssert.AreEqual(new[] { typeof(UseAnyAbilityReasoner), typeof(ChaseReasoner), typeof(PatrolReasoner) },
                fallback.Brain.Transitions.Select(wire => wire.Target.Reasoner.GetType()), "An unknown preset: the melee chaser");
        }

        // ---------- the rig ----------

        private List<GraphIssue> Validate(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return GraphValidator.Validate(asset);
        }

        private static Transition Arrow(AgentState from, AgentState to) => from.Transitions.Single(arrow => arrow.Target == to);

        private static AgentState Do(AgentState decision) =>
            decision.Transitions.First(arrow => arrow.Trigger == TransitionTrigger.Always).Target;

        private static AgentState Next(AgentState block) =>
            block.Transitions.First(arrow => arrow.Trigger == TransitionTrigger.TasksSucceeded).Target;

        private static void AssertSlot(AgentCondition condition, AttackSlotIs @is, string pool)
        {
            var slot = condition as AttackSlotCondition;
            Assert.IsNotNull(slot, "An Attack Slot wired in");
            Assert.AreEqual(@is, slot.Is);
            Assert.AreEqual(pool, slot.Pool);
        }

        // Nodes of no registered type (their titles are their class names in words)
        private sealed class HoldTask : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        private sealed class FlagReasoner : ReasonerTask
        {
            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                whyNot = null;
                return false;
            }
        }

        private sealed class MySensorTask : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        private sealed class ABCValue : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        private sealed class KeepFormationTask : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        // A node whose field was renamed (Weight → Chance) and which holds a list of objects with a field of the old name
        [Serializable]
        private sealed class RenamedNode : AgentTask
        {
            [FormerlySerializedAs("Weight")] public float Chance = 1f;
            public List<RandomOutput> Outputs = new();

            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Success;
        }

#pragma warning disable CS0649, CS0414
        // Link fields: a private one with [SerializeField], a readonly one (no link), a condition, a list of values
        [Serializable]
        private class Linked : AgentTask
        {
            [SerializeField] private AgentTask _inner;
            public readonly AgentTask Fixed = null;
            [NonSerialized, FormerlySerializedAs("OldGate")] public AgentCondition Gate;
            [NonSerialized] public List<AgentValue> Values;

            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Success;
        }

        [Serializable]
        private sealed class LinkedChild : Linked
        {
            [NonSerialized] public AgentTask Extra;
        }
#pragma warning restore CS0649, CS0414
    }
}
