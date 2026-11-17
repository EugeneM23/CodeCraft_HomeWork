using System;
using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The editor's helpers around the canvas: the ready set of reactions (never into a squad's command graph; where they
    /// go when nothing attacks; without Detection, no Hit By); the migration of a character's scene objects (an object of
    /// another kind keeps its kind, a binding to nothing is skipped); the setup wizard run twice; what the character's
    /// inspector says about weapons and Switch Weapon; breakpoints of graphs that are no file; the kinds of an ability's
    /// parts.
    /// </summary>
    public sealed class CanvasEditorPartsTests
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
        }

        private T Made<T>(T made) where T : Object
        {
            _created.Add(made);
            return made;
        }

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            BehaviorGraphAsset asset = Made(ScriptableObject.CreateInstance<BehaviorGraphAsset>());
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        // ---------- the ready set of reactions ----------

        [Test]
        public void ReactionTemplate_ASquadsCommandGraph_GetsNoCharacterReaction()
        {
            var g = new BrainGraphBuilder(new SquadBrainTask());
            g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 8f }) { Tag = "Player", StoreIn = "Foe" });
            g.Variable("Foe", VariableType.Transform);
            g.Chain(g.Decision(new GatherDecision(), 1), new MarchToTargetTask { Target = "Foe" });
            BehaviorGraphAsset asset = Written(g.Build());
            var document = new GraphDocument(asset);
            string before = JsonUtility.ToJson(asset);

            ReactionTemplate.Added added = ReactionTemplate.AddTo(document);

            Assert.IsFalse(added.Any, "A Squad Brain takes a squad's decisions only");
            Assert.IsFalse(document.States().Any(state => state.Reasoner is TargetAttacksReaction or GotHitReaction));
            Assert.AreEqual(before, JsonUtility.ToJson(asset), "The command graph is left as it was");
        }

        [Test]
        public void ReactionTemplate_MakeRoom_NoDecisionAttacks_TheMostImportantsPlace()
        {
            var g = new BrainGraphBuilder();
            AgentState patrol = g.Decision(new PatrolReasoner(), 2);
            AgentState chase = g.Decision(new ChaseReasoner(), 5);
            g.Build();

            int place = ReactionTemplate.MakeRoom(g.Brain, 2);

            Assert.AreEqual(2, place, "No chain attacks: the place of the most important decision");
            Assert.AreEqual(4, patrol.Reasoner.Priority, "It and every less important one move down by the room made");
            Assert.AreEqual(7, chase.Reasoner.Priority);
        }

        [Test]
        public void ReactionTemplate_NoDetection_ReactionsAddedNoHitBy()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new FightReasoner(), 1), new AttackTask());
            var document = new GraphDocument(Written(g.Build()));

            ReactionTemplate.Added added = ReactionTemplate.AddTo(document);

            Assert.IsNotNull(added.TargetAttacks);
            Assert.IsNotNull(added.GotHit);
            Assert.IsNull(added.HitBy, "No Detection to give the Hit By shape to");
            Assert.IsNull(document.Detection(), "…and none is made for it");
        }

        // ---------- a character's scene objects ----------

        [Test]
        public void SceneObjectsMigration_AnAIObjectOfAnotherKind_KeepsIt_ABindingToNothingIsSkipped()
        {
            var go = Made(new GameObject("Worker") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            var home = Made(new GameObject("House") { hideFlags = HideFlags.HideAndDontSave });
            var marker = home.AddComponent<AIObject>();
            marker.Kind = "Home";
            var fields = new SerializedObject(character);
            SerializedProperty list = fields.FindProperty("_bindings");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).FindPropertyRelative(nameof(ObjectBinding.Variable)).stringValue = "Work";
            list.GetArrayElementAtIndex(0).FindPropertyRelative(nameof(ObjectBinding.Object)).objectReferenceValue = home.transform;
            list.GetArrayElementAtIndex(1).FindPropertyRelative(nameof(ObjectBinding.Variable)).stringValue = "X";
            list.GetArrayElementAtIndex(1).FindPropertyRelative(nameof(ObjectBinding.Object)).objectReferenceValue = null;
            fields.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("of the kind 'Home' already"));

            int moved = SceneObjectsMigration.Migrate(character);

            Assert.AreEqual(1, moved, "The binding to nothing is skipped");
            Assert.AreEqual("Home", marker.Kind, "One object, one kind: it keeps its own");
            Assert.IsTrue(marker.IsOwnedBy(character), "…and is the character's now");
            Assert.AreEqual(1, home.GetComponents<AIObject>().Length, "No second AI Object on it");
            Assert.AreEqual(0, new SerializedObject(character).FindProperty("_bindings").arraySize, "The bindings are cleared");
        }

        [Test]
        public void SceneObjectsMigration_ASceneWithNothingToMove_IsNotMarkedChanged()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;

            Assert.AreEqual(0, SceneObjectsMigration.Migrate(scene));
            Assert.AreEqual(dirty, scene.isDirty);
            Assert.AreEqual(0, SceneObjectsMigration.Migrate(default(Scene)), "No scene: nothing");
            Assert.AreEqual(0, SceneObjectsMigration.Migrate((AICharacter)null));
        }

        // ---------- the setup wizard ----------

        [Test]
        public void AgentSetup_RunTwice_TheSecondAddsNothing()
        {
            var grunt = Made(new GameObject("Grunt"));

            AgentSetupContext first = AgentSetup.Run(grunt);
            AgentSetupContext second = AgentSetup.Run(grunt);

            CollectionAssert.Contains(first.Log, "+ AICharacter on Grunt");
            CollectionAssert.Contains(first.Log, "+ Simple Body on Grunt", "Nobody knew the body: a Simple Body");
            CollectionAssert.IsEmpty(second.Log, "Again: only what is missing is added — nothing");
            Assert.AreEqual(1, grunt.GetComponents<AICharacter>().Length);
            Assert.Throws<ArgumentNullException>(() => AgentSetup.Run(null));

            List<IAgentSetupStep> steps = AgentSetup.Steps();
            Assert.AreEqual(0, steps[0].Order, "The character first");
            Assert.IsTrue(steps.Any(step => step.Order == 900), "The fallback body");
            for (int i = 1; i < steps.Count; i++)
            {
                bool inOrder = steps[i - 1].Order < steps[i].Order || steps[i - 1].Order == steps[i].Order &&
                    string.CompareOrdinal(steps[i - 1].GetType().FullName, steps[i].GetType().FullName) <= 0;
                Assert.IsTrue(inOrder, $"Step {i}: by Order, then by name");
            }
        }

        // ---------- the character's inspector ----------

        private AICharacter Carrying(BehaviorGraphAsset graph, params WeaponAsset[] weapons)
        {
            var go = Made(new GameObject("Fighter") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.GraphAsset = graph;
            foreach (WeaponAsset weapon in weapons)
                character.Weapons.Add(new CharacterWeapon { Weapon = weapon });
            return character;
        }

        private WeaponAsset Weapon(string name, params string[] moves)
        {
            WeaponAsset weapon = Made(WeaponAsset.Create(moves.Select(move => new WeaponMove { Name = move }).ToArray()));
            weapon.name = name;
            return weapon;
        }

        [Test]
        public void AICharacterEditor_SwitchWeaponProblems_AndDedup()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new DailyRoutineReasoner(), 1),
                new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT }, new SwitchWeaponTask { Weapon = "Bow" },
                new SwitchWeaponTask { Weapon = "" }, new AttackTask { Move = "Slash" }, new AttackTask { Move = "Slash" });
            BehaviorGraphAsset graph = Written(g.Build());
            WeaponAsset sword = Weapon("Sword", "Slash");
            WeaponAsset bow = Weapon("Bow", "Shoot");

            List<string> alone = AICharacterEditor.Problems(Carrying(graph, sword));

            CollectionAssert.Contains(alone, "Switch Weapon (Next) needs two weapons on the character (Combat section, Weapons).");
            CollectionAssert.Contains(alone, "The character does not carry 'Bow' (Combat section, Weapons).");
            CollectionAssert.Contains(alone, "Switch Weapon names no weapon.");
            Assert.IsFalse(alone.Any(problem => problem.Contains("Slash")), "The sword has the move");

            List<string> both = AICharacterEditor.Problems(Carrying(graph, sword, bow));

            Assert.AreEqual(1, both.Count(problem => problem == "No move 'Slash' on: Bow."), "Said once, naming only the bow");
            Assert.IsFalse(both.Any(problem => problem.StartsWith("Switch Weapon (Next)")), "Two weapons: Next has one to go to");
            Assert.IsFalse(both.Any(problem => problem.Contains("does not carry")));
            CollectionAssert.IsEmpty(AICharacterEditor.Problems(null));
        }

        // ---------- breakpoints ----------

        [Test]
        public void GraphBreakpoints_TwoUnsavedGraphs_DoNotShareBreakpoints()
        {
            BehaviorGraphAsset a = Made(ScriptableObject.CreateInstance<BehaviorGraphAsset>());
            BehaviorGraphAsset b = Made(ScriptableObject.CreateInstance<BehaviorGraphAsset>());
            try
            {
                GraphBreakpoints.Set(a, 5, true);

                Assert.IsTrue(GraphBreakpoints.Has(a, 5));
                Assert.IsFalse(GraphBreakpoints.Has(b, 5), "Each graph its own breakpoints");
                GraphBreakpoints.Set(null, 5, true);
                Assert.IsFalse(GraphBreakpoints.Has(null, 5));
            }
            finally
            {
                GraphBreakpoints.Set(a, 5, false);
            }
        }

        // ---------- an ability's parts ----------

        [Test]
        public void AbilityAssetEditor_KindsOf_ConcreteWithAConstructor_ByClassName()
        {
            List<Type> effects = AbilityAssetEditor.KindsOf(typeof(AbilityEffect));
            List<Type> deliveries = AbilityAssetEditor.KindsOf(typeof(AbilityDelivery));

            Assert.IsFalse(effects.Concat(deliveries).Any(type => type.IsAbstract), "Only what can be made");
            Assert.IsTrue(effects.Concat(deliveries).All(type => type.GetConstructor(Type.EmptyTypes) != null));
            Assert.Less(effects.IndexOf(typeof(AreaDamageEffect)), effects.IndexOf(typeof(DamageEffect)), "By class name");
            Assert.IsTrue(effects.IndexOf(typeof(AreaDamageEffect)) >= 0);
            CollectionAssert.Contains(deliveries, typeof(SelfDelivery));
            Assert.AreEqual("Self", AbilityAssetEditor.TitleOf(typeof(SelfDelivery)));
            Assert.AreEqual("Area Damage", AbilityAssetEditor.TitleOf(typeof(AreaDamageEffect)));
        }
    }
}
