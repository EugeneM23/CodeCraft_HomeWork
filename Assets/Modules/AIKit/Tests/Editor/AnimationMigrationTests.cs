using System.Collections.Generic;
using AIKit.Editor;
using AIKit.Mecanim;
using AIKit.Mecanim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Step 90 moves the character's picture to the AI Animator (C22) and its senses to the graph (C21) without losing
    /// what scenes saved before hold: the migration puts an AI Animator with the old Animation section's settings on the
    /// Animator's object, keeps the faction, and empties the old data so it never runs twice. Setup Agent gives a new
    /// character with an Animator its AI Animator; the inspector says when one is missing.
    /// </summary>
    public sealed class AnimationMigrationTests
    {
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        // A character as a scene saved before step 90 has it: the Animation section on, with these settings
        private AICharacter OldCharacter(out Animator animator)
        {
            var root = new GameObject("Villager") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(root);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            animator = model.AddComponent<Animator>();
            var controller = new AnimatorController();
            _made.Add(controller);
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            animator.runtimeAnimatorController = controller;
            var character = root.AddComponent<AICharacter>();

            var data = new SerializedObject(character);
            data.FindProperty("_animated").boolValue = true;
            data.FindProperty("_animation.Animator").objectReferenceValue = animator;
            data.FindProperty("_animation.Locomotion").stringValue = "Idle";
            data.FindProperty("_animation.Blend").floatValue = 0.3f;
            data.FindProperty("_animation.ClipSpeed").floatValue = 1.596f;
            data.FindProperty("_animation.Smoothing").floatValue = 0.04f;
            data.FindProperty("_animation.Combat").stringValue = "In Combat";
            data.FindProperty("_perception.Faction").intValue = 3;
            data.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }

        [Test]
        public void Migrate_TheOldAnimationSection_BecomesAnAIAnimatorOnTheAnimator_WithItsSettings_OnceOnly()
        {
            AICharacter character = OldCharacter(out Animator animator);
            Assert.That(character.LegacyAnimation, Is.Not.Null, "set-up: the old section is there");

            var log = new List<string>();
            Assert.IsTrue(AnimationMigration.Migrate(character, log));

            var shown = animator.GetComponent<AIAnimator>();
            Assert.That(shown, Is.Not.Null, "an AI Animator on the Animator's own object");
            Assert.That(shown.States.Locomotion, Is.EqualTo("Idle"));
            Assert.That(shown.States.Blend, Is.EqualTo(0.3f));
            Assert.That(shown.Tuning.ClipSpeed, Is.EqualTo(1.596f));
            Assert.That(shown.Tuning.Smoothing, Is.EqualTo(0.04f));
            Assert.That(shown.Parameters.Combat, Is.EqualTo("In Combat"), "the names it wrote, as they were");
            Assert.That(character.Faction, Is.EqualTo(3), "the old Senses section's faction is the character's");
            Assert.That(character.LegacyAnimation, Is.Null, "the old data is emptied");
            Assert.That(character.LegacyFaction, Is.Zero);

            Assert.IsFalse(AnimationMigration.Migrate(character, log), "a second run has nothing to move");
            Assert.That(animator.GetComponents<AIAnimator>().Length, Is.EqualTo(1));
        }

        [Test]
        public void Migrate_ACharacterThatWasNotAnimated_GetsNoAIAnimator()
        {
            AICharacter character = OldCharacter(out Animator animator);
            var data = new SerializedObject(character);
            data.FindProperty("_animated").boolValue = false;
            data.FindProperty("_perception.Faction").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(AnimationMigration.Migrate(character, null));
            Assert.That(animator.GetComponent<AIAnimator>(), Is.Null, "it showed nothing before: it shows nothing now");
        }

        [Test]
        public void SetupAgent_ACharacterWithAnAnimator_GetsAnAIAnimatorOnIt()
        {
            var root = new GameObject("Hero") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(root);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<Animator>();

            AgentSetupContext setup = AgentSetup.Run(root);

            Assert.That(model.GetComponent<AIAnimator>(), Is.Not.Null, string.Join("\n", setup.Log));
            Assert.That(root.GetComponent<AICharacter>(), Is.Not.Null);
        }

        [Test]
        public void Inspector_AnAnimatorWithNoAIAnimator_IsAProblem_WithOneItIsNot()
        {
            AICharacter character = OldCharacter(out Animator animator);
            var hook = new AnimationEditorHook();

            StringAssert.Contains("no AI Animator", hook.Problem(character));
            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains("no AI Animator"),
                "the character's inspector lists it (Validate All too)");

            animator.gameObject.AddComponent<AIAnimator>();
            Assert.That(hook.Problem(character), Is.Null);
        }
    }
}
