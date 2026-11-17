using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The upgrade of step 90 (C7): a graph saved before it — its place checks hidden in the checkboxes Needs Attack Place
    /// / Only When Not First / Ask For Place (an absent key meant "on") — is read into the new picture: wired Attack Slot
    /// conditions and a Take Attack Slot on Circle's On Tick, next to their decisions; the same agents attack and wait as
    /// before; the step runs once.
    /// </summary>
    public sealed class AttackSlotUpgradeTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;
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

        // ---------- an old graph ----------

        // The decisions of an old graph: (decision, the old keys added to its record — null: none at all)
        private BehaviorGraphAsset OldGraph(params (ReasonerTask Decision, string Keys, AgentTask Block)[] decisions)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            int priority = 1;
            foreach ((ReasonerTask decision, string _, AgentTask block) in decisions)
                g.Do(g.Decision(decision, priority++), g.Block(block ?? new WaitTask(100f)));

            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(g.Build(), asset);
            foreach ((ReasonerTask decision, string keys, AgentTask _) in decisions)
            {
                GraphRecord record = asset.Find(decision.Id);
                if (!string.IsNullOrEmpty(keys))
                    record.Data = record.Data.Substring(0, record.Data.Length - 1) + "," + keys + "}";
            }

            asset.FormatVersion = 0;                       // written before step 90
            return asset;
        }

        private static BehaviorGraph Read(BehaviorGraphAsset asset, out List<GraphIssue> issues)
        {
            issues = new List<GraphIssue>();
            BehaviorGraph graph = GraphSerializer.Read(asset, issues);
            Assert.IsNotNull(graph, string.Join("\n", issues));
            return graph;
        }

        private static T Decision<T>(BehaviorGraph graph, int index = 0) where T : ReasonerTask =>
            graph.Root.Children.Where(node => node.Reasoner is T).Select(node => (T)node.Reasoner).ElementAt(index);

        private static AttackSlotCondition Slot(AgentCondition condition) => condition switch
        {
            AttackSlotCondition slot => slot,
            AllCondition all => all.Conditions.OfType<AttackSlotCondition>().SingleOrDefault(),
            _ => null
        };

        // ---------- what an old record becomes ----------

        [Test]
        public void Upgrade_FightAndChase_NeedsAttackPlaceOnOrAbsent_AWiredAttackSlotMine_OffNone()
        {
            BehaviorGraphAsset asset = OldGraph(
                (new FightReasoner(), "\"NeedsAttackPlace\":true", null),
                (new FightReasoner(), null, null),
                (new FightReasoner(), "\"NeedsAttackPlace\":false", null),
                (new ChaseReasoner(), "\"NeedsAttackPlace\":true", null),
                (new ChaseReasoner(), null, null),
                (new ChaseReasoner(), "\"NeedsAttackPlace\":false", null));

            BehaviorGraph graph = Read(asset, out _);

            Assert.IsTrue(asset.UpgradedOnRead, "The editor saves the upgraded graph");
            Assert.AreEqual(AttackSlotIs.Mine, Slot(Decision<FightReasoner>(graph, 0).Condition)?.Is, "On");
            Assert.AreEqual(AttackSlotIs.Mine, Slot(Decision<FightReasoner>(graph, 1).Condition)?.Is,
                "No key: the old default was on");
            Assert.IsNull(Decision<FightReasoner>(graph, 2).Condition, "Off: nothing is wired");
            Assert.AreEqual(AttackSlotIs.Mine, Slot(Decision<ChaseReasoner>(graph, 0).Condition)?.Is);
            Assert.AreEqual(AttackSlotIs.Mine, Slot(Decision<ChaseReasoner>(graph, 1).Condition)?.Is);
            Assert.IsNull(Decision<ChaseReasoner>(graph, 2).Condition);
        }

        [Test]
        public void Upgrade_Flank_SomeoneElseAttacksFirst_OrMineWhenOnlyWhenNotFirstWasOff()
        {
            BehaviorGraphAsset asset = OldGraph(
                (new FlankReasoner(), "\"NeedsAttackPlace\":true,\"OnlyWhenNotFirst\":true", null),
                (new FlankReasoner(), null, null),
                (new FlankReasoner(), "\"OnlyWhenNotFirst\":false", null),
                (new FlankReasoner(), "\"NeedsAttackPlace\":false,\"OnlyWhenNotFirst\":false", null));

            BehaviorGraph graph = Read(asset, out _);

            Assert.AreEqual(AttackSlotIs.MineAndSomeoneAttacksFirst, Slot(Decision<FlankReasoner>(graph, 0).Condition)?.Is);
            Assert.AreEqual(AttackSlotIs.MineAndSomeoneAttacksFirst, Slot(Decision<FlankReasoner>(graph, 1).Condition)?.Is);
            Assert.AreEqual(AttackSlotIs.Mine, Slot(Decision<FlankReasoner>(graph, 2).Condition)?.Is);
            Assert.IsNull(Decision<FlankReasoner>(graph, 3).Condition);
        }

        [Test]
        public void Upgrade_Circle_AskForPlace_ATakeAttackSlotOfItsPoolOnItsOnTick_AndNotMine_OffNothing()
        {
            BehaviorGraphAsset asset = OldGraph(
                (new CircleReasoner(), "\"AskForPlace\":true,\"Pool\":\"Melee\"", null),
                (new CircleReasoner(), "\"AskForPlace\":false", null));

            BehaviorGraph graph = Read(asset, out _);
            AgentState asking = graph.Root.Children.Where(node => node.Reasoner is CircleReasoner).ElementAt(0);
            AgentState silent = graph.Root.Children.Where(node => node.Reasoner is CircleReasoner).ElementAt(1);

            AttackSlotCondition notMine = Slot(asking.Reasoner.Condition);
            Assert.AreEqual(AttackSlotIs.NotMine, notMine?.Is, "Its built-in \"has a place — does not hold\"");
            Assert.AreEqual("Melee", notMine.Pool);
            Transition tick = asking.Transitions.Single(arrow => arrow.Trigger == TransitionTrigger.Tick);
            var ask = (TakeAttackSlotTask)tick.Target.Tasks.Single();
            Assert.AreEqual("Melee", ask.Pool, "It asks the group it asked before");
            Assert.IsTrue(ask.IsInstant, "Wait Time 0: the instant ask");
            Assert.IsTrue(graph.Runs(tick.Target), "Circle's chain reaches it: it runs");

            Assert.IsNull(silent.Reasoner.Condition);
            Assert.IsFalse(silent.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.Tick));
        }

        [Test]
        public void Upgrade_ADecisionThatHadAConditionOfItsOwn_KeepsIt_JoinedToTheSlotByAnAnd()
        {
            var own = new CompareCondition("Rage", NumberComparison.Greater, 2f);
            BehaviorGraphAsset asset = OldGraph((new FightReasoner { Condition = own }, null, null));

            BehaviorGraph graph = Read(asset, out _);
            var both = Decision<FightReasoner>(graph).Condition as AllCondition;

            Assert.IsNotNull(both);
            Assert.AreEqual(2, both.Conditions.Count);
            Assert.IsTrue(both.Conditions.Exists(condition => condition is CompareCondition { Variable: "Rage" }));
            Assert.AreEqual(AttackSlotIs.Mine, Slot(both)?.Is);
        }

        [Test]
        public void Upgrade_Written_IsOfTheCurrentFormat_ReadAgain_NothingIsAddedTwice()
        {
            BehaviorGraphAsset asset = OldGraph(
                (new FightReasoner(), null, null),
                (new CircleReasoner(), null, null));
            BehaviorGraph graph = Read(asset, out _);

            GraphSerializer.Write(graph, asset);
            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion);
            BehaviorGraph again = Read(asset, out _);

            Assert.IsFalse(asset.UpgradedOnRead, "Of the current format: the step does not run");
            Assert.IsInstanceOf<AttackSlotCondition>(Decision<FightReasoner>(again).Condition, "One condition, not an AND of two");
            AgentState circle = again.Root.Children.Single(node => node.Reasoner is CircleReasoner);
            Assert.AreEqual(1, circle.Transitions.Count(arrow => arrow.Trigger == TransitionTrigger.Tick));

            var step = new AttackSlotUpgrade();
            var elements = new Dictionary<int, object>();
            again.VisitNodes(node => elements[node.Id] = node);
            asset.FormatVersion = 0;
            Assert.IsFalse(step.Apply(asset, again, elements, new List<GraphIssue>()),
                "Run on a graph that has its slots already: it changes nothing");
        }

        [Test]
        public void Upgrade_TheNewNodesStandNextToTheirDecision_NeverAtZero()
        {
            BehaviorGraphAsset asset = OldGraph((new FightReasoner(), null, null), (new CircleReasoner(), null, null));
            foreach (GraphRecord record in asset.Records)
                record.Position += new Vector2(500f, 700f);             // nothing of the old graph is at (0, 0)
            BehaviorGraph graph = Read(asset, out _);

            GraphSerializer.Write(graph, asset);

            AgentState fight = graph.Root.Children.Single(node => node.Reasoner is FightReasoner);
            AgentState circle = graph.Root.Children.Single(node => node.Reasoner is CircleReasoner);
            AgentState ask = circle.Transitions.Single(arrow => arrow.Trigger == TransitionTrigger.Tick).Target;
            Vector2 fightAt = asset.Find(fight.Id).Position;
            Assert.AreEqual(fightAt + AttackSlotUpgrade.CONDITION_OFFSET, asset.Find(fight.Reasoner.Condition.Id).Position,
                "The condition stands beside its decision");
            Assert.AreEqual(asset.Find(circle.Id).Position + AttackSlotUpgrade.ASK_OFFSET, asset.Find(ask.Id).Position,
                "The ask stands beside Circle");
        }

        [Test]
        public void Upgrade_OpenedOnTheCanvas_IsSavedUpgraded_TheAskBesideCircle()
        {
            BehaviorGraphAsset asset = OldGraph((new CircleReasoner(), null, null));
            foreach (GraphRecord record in asset.Records)
                record.Position += new Vector2(300f, 300f);

            var document = new GraphDocument(asset);

            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Opened once: saved in the current format");
            AgentState circle = document.Graph.Root.Children.Single(node => node.Reasoner is CircleReasoner);
            AgentState ask = circle.Transitions.Single(arrow => arrow.Trigger == TransitionTrigger.Tick).Target;
            Assert.AreEqual(document.PositionOf(circle) + AttackSlotUpgrade.ASK_OFFSET, document.PositionOf(ask));
            Assert.AreNotEqual(Vector2.zero, asset.Find(circle.Reasoner.Condition.Id).Position);
        }

        // ---------- the same behavior ----------

        [Test]
        public void Upgrade_AnOldGraphOnACrowd_OneAttacks_TheOthersWait_AFreedPlaceGoesToAWaiter()
        {
            BehaviorGraphAsset asset = OldGraph(
                (new FightReasoner { Closer = 100f, LetGoAfter = 100f }, "\"NeedsAttackPlace\":true", new WaitTask(100f)),
                (new CircleReasoner(), "\"AskForPlace\":true", new WaitTask(100f)));
            BehaviorGraph graph = Read(asset, out _);
            var world = new AIWorld();
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.AddComponent<AttackSlotCapacity>().Slots = 1;
            var crowd = new List<GraphRig>();
            for (int i = 0; i < 3; i++)
            {
                var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
                rig.Agent.gameObject.SetActive(true);
                rig.Actuator.Position = new Vector3(2f + i, 0f, 0f);
                rig.Agent.transform.position = rig.Actuator.Position;
                rig.SetGraph(graph);
                rig.Agent.Blackboard.Set(TARGET, go.transform);
                crowd.Add(rig);
            }

            Steps(world, crowd, 2f);
            Assert.AreEqual(1, crowd.Count(Attacks), "As many attack as the target lets: one");
            Assert.AreEqual(2, crowd.Count(rig => rig.Instance.Decision?.Reasoner is CircleReasoner), "The others wait");

            GraphRig attacker = crowd.Single(Attacks);
            new ReleaseAttackSlotTask().Tick(attacker.Context, DT);
            attacker.Agent.Blackboard.Set(TARGET, (Transform)null);     // it has had its turn: it leaves the fight
            Steps(world, crowd, 2f);

            Assert.AreEqual(1, crowd.Count(Attacks), "The freed place went to one who waited for it");
            Assert.IsFalse(Attacks(attacker));
        }

        private static bool Attacks(GraphRig rig) => rig.Instance.Decision?.Reasoner is FightReasoner;

        private static void Steps(AIWorld world, List<GraphRig> crowd, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
            {
                world.BeginStep(DT);
                foreach (GraphRig rig in crowd)
                    rig.Step(DT);
            }
        }
    }
}
