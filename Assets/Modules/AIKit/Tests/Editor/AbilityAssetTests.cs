using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// An ability as an asset made of parts (step 90): what the parts tell the agent's abilities (the description), that an
    /// asset of the old flat numbers turns into parts once — read from the file the way Unity reads it — and that the
    /// asset is shared data: the run and the pause are each character's.
    /// </summary>
    public sealed class AbilityAssetTests
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

        [Test]
        public void AbilityAsset_Describe_ItsPartsAreWhatTheAgentChecks()
        {
            AbilityAsset ability = Made("Fireball",
                new RangeCondition { Min = 2f, Max = 7f }, new LineCondition(), new GroundCondition(),
                new CooldownCondition { Seconds = 4f }, new StartDelayCondition { Seconds = 1.5f },
                new AngleCondition { Front = true, Side = false, Back = false }, new ProjectileDelivery(),
                new DamageEffect { Damage = 3f });
            ability.Queue = new AbilityQueue { AtOnce = 2, Pool = "Spells", Weight = 4f, RepeatPenalty = 0.25f };
            ability.Timing = new AbilityTiming { WindUp = 0.7f, Recovery = 0.3f };
            var definition = new AbilityDefinition();

            ability.Describe(definition);

            Assert.AreEqual("Fireball", definition.Id);
            Assert.AreEqual(2f, definition.Range.Min);
            Assert.AreEqual(7f, definition.Range.Max);
            Assert.IsTrue(definition.RequiresLineOfSight);
            Assert.IsTrue(definition.RequiresGround);
            Assert.AreEqual(4f, definition.Cooldown);
            Assert.AreEqual(1.5f, definition.StartDelay);
            Assert.AreEqual(AbilityAngles.Front, definition.Angles);
            Assert.AreEqual("Spells", definition.TokenPool, "Its queue's pool, shared by name");
            Assert.AreEqual(2, definition.TokenCapacity);
            Assert.AreEqual(4f, definition.Weight);
            Assert.AreEqual(0.25f, definition.RepeatPenalty);
            Assert.AreEqual(0.7f, definition.WindUp);
            Assert.AreEqual(0.3f, definition.Recovery);
            Assert.IsTrue(definition.RequiresTarget);
        }

        [Test]
        public void AbilityAsset_NoConditions_AnyDistanceAnySide_AQueueWithNoPool_IsNoLimit()
        {
            AbilityAsset ability = Made("Shout", new SelfDelivery());
            ability.Queue.AtOnce = 0;
            var definition = new AbilityDefinition();

            ability.Describe(definition);

            Assert.IsTrue(definition.InRange(500f), "No Range part: no distance it is kept from");
            Assert.AreEqual(AbilityAngles.All, definition.Angles);
            Assert.AreEqual("", definition.TokenPool, "At Once 0: it takes no turns");
            Assert.IsFalse(definition.RequiresTarget, "On itself: no target needed");
            Assert.IsFalse(definition.RequiresGround);
        }

        [Test]
        public void AbilityAsset_AtOnceWithNoPoolName_TakesTurnsInAPoolOfItsOwnName()
        {
            AbilityAsset ability = Made("Grenade", new LobDelivery());
            ability.Queue = new AbilityQueue { AtOnce = 1, Pool = "" };
            var definition = new AbilityDefinition();

            ability.Describe(definition);

            Assert.AreEqual("Grenade", definition.TokenPool);
            Assert.AreEqual(1, definition.TokenCapacity);
        }

        [Test]
        public void AbilityAsset_OldFlatNumbers_TurnIntoParts_Once()
        {
            var ability = ScriptableObject.CreateInstance<AbilityAsset>();
            _created.Add(ability);
            const string OLD = "{\"Name\":\"Smash\",\"_oldMinRange\":1.0,\"_oldMaxRange\":3.0,\"_oldCooldown\":5.0," +
                               "\"_oldNeedsSight\":true,\"_oldWindUp\":0.4,\"_oldRecovery\":0.2,\"_oldDamage\":2.5," +
                               "\"_oldWeight\":1.5,\"_oldAtOnce\":2,\"_neededMyPlace\":true}";

            JsonUtility.FromJsonOverwrite(OLD, ability);

            Assert.IsTrue(ability.ConvertedOnLoad, "Read from the old numbers: turned into parts");
            Assert.AreEqual(1f, ability.Find<RangeCondition>().Min);
            Assert.AreEqual(3f, ability.Find<RangeCondition>().Max);
            Assert.IsNotNull(ability.Find<GroundCondition>(), "The old abilities were used from the ground only");
            Assert.IsNotNull(ability.Find<LineCondition>(), "Needs Sight");
            Assert.AreEqual(5f, ability.Cooldown);
            Assert.AreEqual(0.4f, ability.Timing.WindUp);
            Assert.AreEqual(0.2f, ability.Timing.Recovery);
            Assert.AreEqual(2, ability.Queue.AtOnce);
            Assert.AreEqual(1.5f, ability.Queue.Weight);
            Assert.AreEqual(3f, ((StrikeDelivery)ability.Delivery).Reach, "A blow at its Max Range, as the old OnStrike");
            Assert.AreEqual(2.5f, ability.Find<DamageEffect>().Damage);
            Assert.IsTrue(ability.NeededMyPlace, "Kept for the graph upgrade (an Attack Slot condition)");

            // Written and read again: parts, no second conversion
            ability.ConvertedOnLoad = false;
            ability.Find<RangeCondition>().Max = 6f;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ability), ability);

            Assert.IsFalse(ability.ConvertedOnLoad, "Once only");
            Assert.AreEqual(6f, ability.Find<RangeCondition>().Max, "The parts as they were written");
        }

        [Test]
        public void AbilityAsset_AFileOfTheOldFormat_IsReadIntoParts_ByItsOldFieldNames()
        {
            const string PATH = "Assets/__AIKit_OldAbility_Test.asset";
            var made = ScriptableObject.CreateInstance<AbilityAsset>();
            string script = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(made)));
            Object.DestroyImmediate(made);
            string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                          "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                          "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n" +
                          "  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                          $"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n" +
                          "  m_Name: __AIKit_OldAbility_Test\n  m_EditorClassIdentifier: AIKit::AIKit.AbilityAsset\n" +
                          "  Name: Kick\n  MinRange: 0.5\n  MaxRange: 2.5\n  Cooldown: 3\n  NeedsSight: 0\n" +
                          "  NeedsMyPlace: 1\n  WindUp: 0.3\n  Recovery: 0.4\n  Damage: 4\n  Weight: 1\n  AtOnce: 0\n";
            try
            {
                System.IO.File.WriteAllText(PATH, yaml);
                AssetDatabase.ImportAsset(PATH, ImportAssetOptions.ForceUpdate);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityAsset>(PATH);

                Assert.IsNotNull(ability);
                Assert.AreEqual("Kick", ability.Name);
                Assert.AreEqual(0.5f, ability.Find<RangeCondition>().Min);
                Assert.AreEqual(2.5f, ability.Find<RangeCondition>().Max);
                Assert.AreEqual(3f, ability.Cooldown);
                Assert.IsNull(ability.Find<LineCondition>());
                Assert.AreEqual(4f, ability.Find<DamageEffect>().Damage);
                Assert.AreEqual(0, ability.Queue.AtOnce, "At Once 0 — any number at once, as before");
                Assert.IsTrue(ability.NeededMyPlace);
            }
            finally
            {
                AssetDatabase.DeleteAsset(PATH);
            }
        }

        [Test]
        public void Inspector_PlusPart_OffersEveryPartOfItsKind_InPlainWords()
        {
            List<System.Type> conditions = AIKit.Editor.AbilityAssetEditor.KindsOf(typeof(AbilityCondition));
            List<System.Type> deliveries = AIKit.Editor.AbilityAssetEditor.KindsOf(typeof(AbilityDelivery));

            CollectionAssert.IsSupersetOf(conditions, new[]
            {
                typeof(RangeCondition), typeof(LineCondition), typeof(GroundCondition), typeof(AngleCondition),
                typeof(CooldownCondition), typeof(StartDelayCondition), typeof(ScoreCondition)
            });
            CollectionAssert.IsSupersetOf(deliveries, new[]
            {
                typeof(StrikeDelivery), typeof(ProjectileDelivery), typeof(LobDelivery), typeof(SelfDelivery),
                typeof(CodeRunnerDelivery)
            });
            Assert.IsFalse(conditions.Contains(typeof(StrikeDelivery)), "Each kind offers its own parts only");
            Assert.AreEqual("Area Damage", AIKit.Editor.AbilityAssetEditor.TitleOf(typeof(AreaDamageEffect)));
            Assert.AreEqual("Ground Mark", AIKit.Editor.AbilityAssetEditor.TitleOf(typeof(GroundMarkCue)));
            Assert.AreEqual("Code Runner", AIKit.Editor.AbilityAssetEditor.TitleOf(typeof(CodeRunnerDelivery)));
        }

        [Test]
        public void AbilityAsset_CodeRunner_LeavesThePlayingToTheGamesRunner_AndItsTurnsToItsDefinition()
        {
            AbilityAsset ability = AbilityAsset.CodeRunner("Strike");
            _created.Add(ability);

            Assert.IsTrue(ability.IsCodeRunner);
            Assert.AreEqual("Strike", ability.Name);
            Assert.AreEqual(0, ability.Queue.AtOnce, "How it takes turns is the game's definition");
        }

        [Test]
        public void AbilityAsset_SharedByTwoCharacters_ThePauseIsEachOnesOwn()
        {
            AbilityAsset ability = Made("Grenade", new CooldownCondition { Seconds = 5f }, new SelfDelivery());
            ability.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            GraphRig first = Rig(ability), second = Rig(ability);

            Assert.IsTrue(first.Abilities.TryUse("Grenade", first.Context));
            first.Step(0.1f, 4);

            Assert.Greater(first.Abilities.CooldownLeft("Grenade"), 0f);
            Assert.AreEqual(0f, second.Abilities.CooldownLeft("Grenade"), "The other character's rest is its own");
            Assert.IsTrue(second.Abilities.IsReady("Grenade", second.Context));
            Assert.AreNotSame(first.Agent.AbilityRunners[0], second.Agent.AbilityRunners[0], "A runner each");
        }

        // A character whose graph names the ability (a decision that never holds, so it only waits)
        private static GraphRig Rig(AbilityAsset ability)
        {
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            AgentState use = g.Decision(new UseAbilityReasoner { Ability = ability });
            g.Chain(use, new UseAbilityTask());
            rig.SetGraph(g.Build());
            return rig;
        }

        private AbilityAsset Made(string name, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            _created.Add(ability);
            return ability;
        }
    }
}
