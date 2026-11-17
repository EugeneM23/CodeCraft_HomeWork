using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The character's abilities are its graph's (step 90, C6 / C23 — no list on the character): given a graph, the
    /// character prepares its own runner for every ability the graph names (decisions, blocks, the Ability nodes of a set),
    /// once per ability; a new graph takes the old graph's runners away and brings its own; an id a game's own runner was
    /// registered under stays the game's; an ability the game plays is only described.
    /// </summary>
    public sealed class CharacterAbilityTests
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
            AbilityAsset ability = AbilityAsset.Create(name, new CooldownCondition { Seconds = 3f }, new SelfDelivery());
            _created.Add(ability);
            return ability;
        }

        [Test]
        public void TheGraphNamesAbilities_TheCharacterHasARunnerForEach_OnceEach()
        {
            AbilityAsset grenade = Ability("Grenade");
            AbilityAsset kick = Ability("Kick");
            AbilityAsset roar = Ability("Roar");
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = grenade }), new UseAbilityTask(grenade));
            g.Chain(g.Decision(new UseAnyAbilityReasoner(kick, roar)), new UseAbilityTask());
            var rig = new GraphRig();

            rig.SetGraph(g.Build());

            Assert.AreEqual(3, rig.Agent.AbilityRunners.Count, "Named twice, prepared once");
            Assert.IsTrue(rig.Abilities.Has("Grenade"));
            Assert.IsTrue(rig.Abilities.Has("Kick"));
            Assert.IsTrue(rig.Abilities.Has("Roar"));
            Assert.IsInstanceOf<AbilityRunner>(rig.Abilities.RunnerOf("Kick"));
        }

        [Test]
        public void TwoCharactersWithOneAsset_KeepTheirOwnPause()
        {
            AbilityAsset grenade = Ability("Grenade");
            grenade.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            BehaviorGraph Graph()
            {
                var g = new BrainGraphBuilder();
                g.Chain(g.Decision(new UseAbilityReasoner { Ability = grenade }), new UseAbilityTask());
                return g.Build();
            }

            var first = new GraphRig();
            var second = new GraphRig();
            first.SetGraph(Graph());
            second.SetGraph(Graph());

            Assert.IsTrue(first.Abilities.TryUse("Grenade", first.Context));
            first.Step(0.1f, 4);

            Assert.Greater(first.Abilities.CooldownLeft("Grenade"), 0f);
            Assert.AreEqual(0f, second.Abilities.CooldownLeft("Grenade"));
            Assert.AreEqual(0, second.Agent.AbilityRunners[0].Uses, "The run is each character's");
        }

        [Test]
        public void ANewGraph_TheOldGraphsRunnersGo_TheNewGraphsCome()
        {
            AbilityAsset grenade = Ability("Grenade");
            AbilityAsset kick = Ability("Kick");
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = grenade }), new UseAbilityTask());
            rig.SetGraph(g.Build());
            Assert.IsTrue(rig.Abilities.Has("Grenade"));

            var other = new BrainGraphBuilder();
            other.Chain(other.Decision(new UseAbilityReasoner { Ability = kick }), new UseAbilityTask());
            rig.SetGraph(other.Build());

            Assert.IsFalse(rig.Abilities.Has("Grenade"), "The graph that named it is gone");
            Assert.IsTrue(rig.Abilities.Has("Kick"));
            Assert.AreEqual(1, rig.Agent.AbilityRunners.Count);
            Assert.AreEqual("Kick", rig.Agent.AbilityRunners[0].Id);
        }

        [Test]
        public void AGamesRunnerRegisteredFirst_StaysTheGames_EvenForANewGraph()
        {
            AbilityAsset strike = Ability("Strike");
            var rig = new GraphRig();
            TestAbilityRunner game = rig.AddAbility("Strike", duration: 0.2f);
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = strike }), new UseAbilityTask());

            rig.SetGraph(g.Build());
            rig.SetGraph(g.Build());

            Assert.AreSame(game, rig.Abilities.RunnerOf("Strike"), "The game's runner plays it");
            Assert.AreEqual(0, rig.Agent.AbilityRunners.Count, "No runner of the character's own for it");
        }

        [Test]
        public void ACodeRunnerAbility_IsDescribed_TheGamesRunnerPlaysItWhenItComes()
        {
            AbilityAsset swipe = AbilityAsset.CodeRunner("Swipe");
            _created.Add(swipe);
            swipe.Conditions.Add(new RangeCondition { Max = 3f });
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAnyAbilityReasoner(swipe)), new UseAbilityTask());

            rig.SetGraph(g.Build());

            Assert.IsTrue(rig.Abilities.Has("Swipe"), "Described from the asset");
            Assert.AreEqual(3f, rig.Abilities.DefinitionOf("Swipe").MaxRange);
            Assert.IsNull(rig.Abilities.RunnerOf("Swipe"), "Its runner is the game's");

            var runner = new TestAbilityRunner("Swipe");
            rig.Abilities.Register(runner);
            Assert.AreSame(runner, rig.Abilities.RunnerOf("Swipe"));
        }
    }
}
