using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIKit.Mecanim.Editor
{
    /// <summary>
    /// Step 90's migration of the characters of the open scenes (C20–C22): the old Animation section of each AI
    /// Character becomes an AI Animator on its Animator's object with the same settings, its old Senses section's
    /// faction goes to its Advanced section, and the climb and the jump of its body are measured from the AI Animator's
    /// clips (the body's old numbers when there is nothing to measure). The old data is emptied afterwards, so the
    /// migration never runs twice on one character. Nothing is saved: look, then save the scenes.
    /// </summary>
    public static class AnimationMigration
    {
        public const string MENU = "Tools/AI Kit/Migrate - Animation To AI Animator";

        [MenuItem(MENU)]
        private static void MigrateOpenScenes()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Migrate: leave Play Mode first.");
                return;
            }

            var log = new List<string>();
            int characters = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (AICharacter character in root.GetComponentsInChildren<AICharacter>(true))
                    {
                        characters++;
                        changed |= Migrate(character, log);
                    }
                }

                if (changed)
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"Migrate - Animation To AI Animator: {characters} characters, {log.Count} changes " +
                      "(the scenes are not saved)\n" + string.Join("\n", log));
        }

        /// <summary>One character moved to the new owners of its old data (with Undo); false — nothing to move.</summary>
        public static bool Migrate(AICharacter character, List<string> log)
        {
            if (character == null)
                return false;

            bool changed = false;
            Undo.RecordObject(character, "Migrate Animation To AI Animator");

            // Animation → AI Animator on the Animator's object, the same settings
            LegacyAnimationSettings legacy = character.LegacyAnimation;
            if (legacy != null)
            {
                Animator animator = legacy.Animator as Animator;
                if (animator != null && !animator.transform.IsChildOf(character.transform))
                {
                    // An AI Animator finds its character above it: on an Animator outside the character it never would
                    log?.Add($"{character.name}: its old Animator ({animator.name}) is outside the character — the AI " +
                             "Animator goes on the Animator under it");
                    animator = null;
                }

                if (animator == null)
                    animator = character.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    log?.Add($"{character.name}: animated, but no Animator under it — nothing shows it");
                }
                else
                {
                    AIAnimator shown = animator.GetComponent<AIAnimator>();
                    if (shown == null)
                        shown = Undo.AddComponent<AIAnimator>(animator.gameObject);
                    else
                        Undo.RecordObject(shown, "Migrate Animation To AI Animator");
                    CopySettings(legacy, shown);
                    EditorUtility.SetDirty(shown);
                    log?.Add($"{character.name}: AI Animator on {animator.name}");
                }

                changed = true;
            }

            // Senses → the faction stays the character's
            if (character.LegacyFaction != 0 && character.Faction == 0)
            {
                character.Faction = character.LegacyFaction;
                log?.Add($"{character.name}: faction {character.Faction}");
                changed = true;
            }

            // The climb and the jump: measured from the clips; the body's old numbers when nothing can be measured
            AgentBody body = character.Body;
            if (body != null && body.Traverses && body.Traversal is { IsMeasured: false })
            {
                string measured = TraversalMeasure.MeasureInto(character);
                bool hadOld = body is ILegacyTraversal had && had.TryTakeLegacyTraversal(out _);
                ILegacyTraversal oldBody = body as ILegacyTraversal;
                if (body.Traversal.IsMeasured)
                {
                    log?.Add($"{character.name}: {measured}");
                    // What the clips could not give is kept from the body's old numbers: a controller with a climbing
                    // clip and no jumping ones jumps as the body did before
                    if (hadOld && oldBody.TryTakeLegacyTraversal(out MeasuredTraversal old))
                    {
                        MeasuredTraversal now = body.Traversal;
                        if (!now.HasJump && old.HasJump)
                        {
                            now.HasJump = true;
                            now.Jump = old.Jump;
                            log?.Add($"{character.name}: the jump kept from the body's old numbers (no jumping clips)");
                        }

                        if (!now.HasClimb && old.HasClimb)
                        {
                            now.HasClimb = true;
                            now.Climb = old.Climb;
                            log?.Add($"{character.name}: the climb kept from the body's old numbers (no climbing clip)");
                        }

                        body.Traversal = now;
                    }
                }
                else if (hadOld && oldBody.TryTakeLegacyTraversal(out MeasuredTraversal kept))
                {
                    body.Traversal = kept;
                    log?.Add($"{character.name}: the climb kept from the body's old numbers ({measured ?? "no AI Animator"})");
                }

                changed = true;
            }

            if (body is ILegacyTraversal legacyBody)
                legacyBody.ClearLegacyTraversal();

            if (legacy != null || character.LegacyFaction != 0)
                character.ClearLegacy();

            if (changed)
            {
                EditorUtility.SetDirty(character);
                if (PrefabUtility.IsPartOfPrefabInstance(character))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(character);
            }

            return changed;
        }

        /// <summary>The old Animation section's settings, as they were, onto an AI Animator.</summary>
        public static void CopySettings(LegacyAnimationSettings from, AIAnimator to)
        {
            AnimatorParameters names = to.Parameters;
            names.Speed = from.Speed;
            names.MoveRate = from.MoveRate;
            names.MoveX = from.MoveX;
            names.MoveZ = from.MoveZ;
            names.Stride = from.Stride;
            names.Backing = from.Backing;
            names.Attack = from.Attack;
            names.Combat = from.Combat;
            names.Braking = from.Braking;
            names.Weapon = from.Weapon;
            names.Climb = from.Climb;
            names.Jump = from.Jump;

            AnimatorStates states = to.States;
            states.Locomotion = from.Locomotion;
            states.ActionTag = from.ActionTag;
            states.Blend = from.Blend;
            states.ClimbUp = from.ClimbUpState;
            states.ClimbDown = from.ClimbDownState;

            AnimatorTuning tuning = to.Tuning;
            tuning.ClipSpeed = from.ClipSpeed;
            tuning.SlowestRate = from.SlowestRate;
            tuning.Smoothing = from.Smoothing;
            tuning.CalmAfter = from.CalmAfter;
            tuning.BrakingAt = from.BrakingAt;
        }
    }
}
