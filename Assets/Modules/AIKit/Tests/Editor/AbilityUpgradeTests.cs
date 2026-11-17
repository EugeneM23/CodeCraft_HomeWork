using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Graphs of before step 90 read into the new picture, never rebuilt: the Throw block and decision (gone) become the
    /// Attack block and decision in the same node, its wires and arrows kept, the replacement named; an ability named by a
    /// string becomes the link to the ability asset of that name (the editor's step), a name no asset has is a problem of
    /// the node; an old string key never breaks the reading.
    /// </summary>
    public sealed class AbilityUpgradeTests
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

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        // The record of the node as an old graph had it: another type, its old data
        private static void MakeOld(BehaviorGraphAsset asset, int id, string type, string data)
        {
            GraphRecord record = asset.Find(id);
            record.Type = type;
            record.Data = data;
            asset.FormatVersion = 0;
            asset.MarkChanged();
        }

        [Test]
        public void ThrowBlockAndDecision_BecomeAttack_InTheSameNodes_TheirWiresKept_AndNamed()
        {
            var g = new BrainGraphBuilder();
            var throwing = new FightReasoner();
            AgentState decision = g.Decision(throwing, 2);
            var chase = new ChaseTask { StopDistance = 9f };
            var stand = new WaitTask(5f);
            AgentState[] chain = g.Chain(decision, chase, stand);
            AgentState patrol = g.Decision(new PatrolReasoner(), 3);
            g.Chain(patrol, new WaitTask(100f));
            BehaviorGraph built = g.Build();
            throwing.NotFrom.Add(patrol.Id);
            BehaviorGraphAsset asset = Written(built);
            MakeOld(asset, stand.Id, ThrowUpgrade.THROW_BLOCK,
                "{\"Name\":\"Throw\",\"Target\":\"Victim\",\"Action\":\"Throw\",\"Range\":{\"_value\":12.0}}");
            MakeOld(asset, throwing.Id, ThrowUpgrade.THROW_DECISION,
                $"{{\"Name\":\"Throw\",\"Priority\":2,\"NotFrom\":[{patrol.Id}]}}");

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            AgentState block = read.Root.Children.Single(state => state.Id == chain[1].Id);
            var attack = block.Tasks.Single() as AttackTask;
            Assert.IsNotNull(attack, "The Throw block is an Attack in the same node");
            Assert.AreEqual("Victim", attack.Target, "Its object kept");
            Assert.AreEqual("", attack.Move, "The weapon's first move (a rock has one)");
            AgentState node = read.Root.Children.Single(state => state.Id == decision.Id);
            var fight = node.Reasoner as FightReasoner;
            Assert.IsNotNull(fight, "The Throw decision is the Attack decision");
            Assert.AreEqual(2, fight.Priority);
            CollectionAssert.AreEqual(new[] { patrol.Id }, fight.NotFrom);
            Assert.IsTrue(read.Brain.Transitions.Exists(wire => wire.Target == node), "Still wired to the Brain");
            Assert.IsTrue(read.Root.Children.Single(state => state.Id == chain[0].Id).Transitions
                .Exists(arrow => arrow.Target == block), "The arrow into it kept");
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.UnknownNodeType),
                "Nothing unread: the graph is not read-only");
            Assert.AreEqual(2, issues.Count(issue => issue.Code == GraphIssueCode.ReplacedNode && !issue.IsError));
            Assert.IsTrue(asset.UpgradedOnRead, "The editor saves the upgraded graph");
        }

        [Test]
        public void ThrowUpgrade_AGraphWithoutThrow_IsLeftAsItIs()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new PatrolReasoner()), new WaitTask(1f));
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;
            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());

            var issues = new List<GraphIssue>();
            Assert.IsFalse(new ThrowUpgrade().Apply(asset, read, new Dictionary<int, object>(), issues));
            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void AbilityNamedByAString_TheEditorsStep_LinksTheAssetOfThatName()
        {
            AbilityAsset grenade = AbilityAsset.Create("Grenade", new SelfDelivery());
            _created.Add(grenade);
            var decision = new UseAbilityReasoner();
            var block = new UseAbilityTask();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), block);
            BehaviorGraph built = g.Build();
            BehaviorGraphAsset asset = Written(built);
            asset.Find(decision.Id).Data = "{\"Name\":\"Use Ability\",\"Priority\":1,\"Ability\":\"Grenade\"}";
            asset.Find(block.Id).Data = "{\"Name\":\"Use Ability\",\"AbilityId\":\"Grenade\"}";
            asset.MarkChanged();

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);
            Assert.IsFalse(issues.Exists(issue => issue.IsError), "An old string key never breaks the reading");

            bool changed = AbilityNameUpgrade.Apply(asset, read, issues, name => name == "Grenade" ? grenade : null);

            Assert.IsTrue(changed);
            var nodes = new List<AgentNode>();
            read.VisitNodes(nodes.Add);
            Assert.AreSame(grenade, nodes.OfType<UseAbilityReasoner>().Single().Ability);
            Assert.AreSame(grenade, nodes.OfType<UseAbilityTask>().Single().Ability);

            GraphSerializer.Write(read, asset);
            Assert.IsFalse(asset.Records.Any(record => record.Data.Contains("\"Grenade\"")), "Saved, the string is gone");
            Assert.IsFalse(AbilityNameUpgrade.Apply(asset, GraphSerializer.Read(asset, new List<GraphIssue>()),
                new List<GraphIssue>(), name => grenade), "Once: nothing left to upgrade");
        }

        [Test]
        public void AbilityNamedByAString_NoAssetOfThatName_IsAProblemOfTheNode()
        {
            var block = new UseAbilityTask();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner()), block);
            BehaviorGraphAsset asset = Written(g.Build());
            asset.Find(block.Id).Data = "{\"Name\":\"Use Ability\",\"AbilityId\":\"Fireball\"}";
            asset.MarkChanged();
            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());
            var issues = new List<GraphIssue>();

            Assert.IsFalse(AbilityNameUpgrade.Apply(asset, read, issues, name => null));

            GraphIssue missing = issues.Single(issue => issue.Code == GraphIssueCode.NoAbility);
            Assert.AreEqual(block.Id, missing.RecordId);
            StringAssert.Contains("Fireball", missing.Message);
        }

        [Test]
        public void AnAbilityThatNeededMyPlace_ItsDecisionGetsAnAttackSlotCondition()
        {
            var ability = ScriptableObject.CreateInstance<AbilityAsset>();
            _created.Add(ability);
            JsonUtility.FromJsonOverwrite("{\"Name\":\"Smash\",\"_oldMaxRange\":2.0,\"_neededMyPlace\":true}", ability);
            var decision = new UseAbilityReasoner();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask());
            BehaviorGraphAsset asset = Written(g.Build());
            asset.Find(decision.Id).Data = "{\"Name\":\"Use Ability\",\"Priority\":1,\"Ability\":\"Smash\"}";
            asset.MarkChanged();
            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());

            AbilityNameUpgrade.Apply(asset, read, new List<GraphIssue>(), name => ability);

            var nodes = new List<AgentNode>();
            read.VisitNodes(nodes.Add);
            AgentCondition condition = nodes.OfType<UseAbilityReasoner>().Single().Condition;
            Assert.IsNotNull(condition);
            Assert.AreEqual(AttackSlotLink.ID, GraphSerializer.TypeIdOf(condition));
        }
    }
}
