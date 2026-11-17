using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Ability assets in a graph asset (step 90): a decision's and a block's ability are links to the asset, kept in the
    /// graph asset's table of objects (never as instance ids in a node's data), and the set of Use Any Ability is Ability
    /// nodes wired into its list pin — all read back as they were written; the agent runs the read graph with them.
    /// </summary>
    public sealed class GraphObjectLinkTests
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

        private AbilityAsset Ability(string name)
        {
            AbilityAsset ability = AbilityAsset.Create(name, new SelfDelivery());
            _created.Add(ability);
            return ability;
        }

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        [Test]
        public void UseAbility_DecisionAndBlock_TheirAbilityLinks_AreReadBack()
        {
            AbilityAsset grenade = Ability("Grenade");
            AbilityAsset kick = Ability("Kick");
            var g = new BrainGraphBuilder();
            AgentState decision = g.Decision(new UseAbilityReasoner { Ability = grenade });
            g.Chain(decision, new UseAbilityTask(kick));
            BehaviorGraphAsset asset = Written(g.Build());

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            var nodes = new List<AgentNode>();
            read.VisitNodes(nodes.Add);
            Assert.AreSame(grenade, nodes.OfType<UseAbilityReasoner>().Single().Ability);
            Assert.AreSame(kick, nodes.OfType<UseAbilityTask>().Single().Ability);
            CollectionAssert.AreEquivalent(new Object[] { grenade, kick }, asset.Objects, "Kept in the table of objects");
            Assert.IsFalse(asset.Records.Any(record => record.Data != null && record.Data.Contains("instanceID")),
                "No instance ids in a node's data");
            CollectionAssert.IsEmpty(issues.Where(issue => issue.IsError));
        }

        [Test]
        public void UseAnyAbility_ItsSet_AbilityNodesWiredIntoItsListPin_AreReadBackInOrder()
        {
            AbilityAsset jab = Ability("Jab");
            AbilityAsset hook = Ability("Hook");
            AbilityAsset kick = Ability("Kick");
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAnyAbilityReasoner(jab, hook, kick)), new UseAbilityTask());
            BehaviorGraphAsset asset = Written(g.Build());

            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());

            var nodes = new List<AgentNode>();
            read.VisitNodes(nodes.Add);
            UseAnyAbilityReasoner any = nodes.OfType<UseAnyAbilityReasoner>().Single();
            CollectionAssert.AreEqual(new[] { jab, hook, kick }, any.Abilities.Select(value => value.Ability).ToArray());
            Assert.AreEqual(3, nodes.OfType<AbilityValue>().Count(), "Each one a node of its own, wired in");
        }

        [Test]
        public void AReadGraph_TheCharacterPreparesTheAbilitiesItNames()
        {
            AbilityAsset jab = Ability("Jab");
            AbilityAsset hook = Ability("Hook");
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = "Target";
            g.Variable("Target", VariableType.Transform);
            g.Chain(g.Decision(new UseAnyAbilityReasoner(jab, hook)), new UseAbilityTask());
            BehaviorGraphAsset asset = Written(g.Build());
            var rig = new GraphRig();

            rig.SetGraph(asset.Compile());

            Assert.IsTrue(rig.Abilities.Has("Jab"));
            Assert.IsTrue(rig.Abilities.Has("Hook"));
        }
    }
}
