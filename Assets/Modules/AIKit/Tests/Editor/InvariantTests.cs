using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 10: the module's invariants (spec §1.4) that live in the source itself, read from the module's files:
    /// no UnityEngine.Random (2), no engine time (3) — except the one place the module drives itself, nodes do not
    /// scan the world (7) — nor do the objects of the world and their registry. The others are held by behavior tests (the list — README, "Инварианты").
    /// </summary>
    public class InvariantTests
    {
        private static string ModuleFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets("AIKit t:AssemblyDefinitionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "AIKit.asmdef")
                    return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
            }

            Assert.Fail("AIKit.asmdef not found");
            return null;
        }

        // The code lines (comments dropped) of the runtime sources of the module under a subfolder
        private static IEnumerable<(string File, int Line, string Text)> Code(params string[] folders)
        {
            string module = ModuleFolder();
            foreach (string folder in folders)
            {
                string root = Path.Combine(module, folder);
                if (!Directory.Exists(root))
                    continue;

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string text = lines[i].Trim();
                        if (text.StartsWith("//"))
                            continue;

                        yield return (Path.GetFileName(file), i + 1, text);
                    }
                }
            }
        }

        private static List<string> Find(IEnumerable<(string File, int Line, string Text)> code, string[] patterns,
            params string[] allowedFiles) =>
            code.Where(line => patterns.Any(line.Text.Contains) && !allowedFiles.Contains(line.File))
                .Select(line => $"{line.File}:{line.Line}: {line.Text}").ToList();

        [Test]
        public void Invariant2_NoUnityRandom_InTheModule()
        {
            List<string> found = Find(Code("Runtime"),
                new[] { "UnityEngine.Random", "Random.Range", "Random.value", "Random.insideUnit" });

            CollectionAssert.IsEmpty(found, "Randomness comes only from the agent's own seeded random");
        }

        [Test]
        public void Invariant3_NoEngineTime_ButTheAgentsOwnStep()
        {
            List<string> found = Find(Code("Runtime"),
                new[] { "Time.time", "Time.deltaTime", "Time.fixedDeltaTime", "Time.unscaled", "StartCoroutine",
                    "Invoke(\"", "WaitForSeconds" },
                "AICharacter.cs", "AIWorldRunner.cs", "AISquad.cs");   // they drive themselves when no loop does

            CollectionAssert.IsEmpty(found, "Time comes only as dt");
        }

        [Test]
        public void Invariant7_NodesDoNotScanTheWorld()
        {
            // The objects of the world too (step 90): they register themselves, the registry never looks for them
            List<string> found = Find(Code("Runtime/Core/Graph", "Runtime/Core/Nodes", "Runtime/Core/Conditions",
                    "Runtime/Core/Objects"),
                new[] { "FindObject", "FindAnyObject", "FindFirstObject", "OverlapCircle", "OverlapSphere",
                    "OverlapBox", "Physics.", "Physics2D.", "GameObject.Find" });

            CollectionAssert.IsEmpty(found, "A node reaches the world through the context, the perception, the roster");
        }

        // ---------- step 90 (C22): the model knows nothing of its picture ----------

        // The code of a line with its string literals and its comment left out: a tooltip or a note may name the AI Animator
        private static string WithoutStrings(string text)
        {
            string code = System.Text.RegularExpressions.Regex.Replace(text, "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");
            int comment = code.IndexOf("//", System.StringComparison.Ordinal);
            return comment >= 0 ? code.Substring(0, comment) : code;
        }

        private static readonly System.Text.RegularExpressions.Regex AnimationTypes = new(
            @"\b(Animator|AnimationClip|RuntimeAnimatorController|AnimatorOverrideController|AnimatorStateInfo|" +
            @"AnimatorControllerParameter\w*)\b");

        [Test]
        public void TheCore_UsesNoAnimationType_ButTheWeaponsClipSet()
        {
            // WeaponAsset.Motions is the one exception: the clip set of a weapon is the weapon's data (the user's word,
            // step 86: "the weapon swaps the animations and keeps the numbers"); the AI Animator reads it
            // (AICharacter.Legacy.cs: the old section's field is named Animator, a plain Component — for the migration)
            List<string> found = Code("Runtime/Core")
                .Where(line => AnimationTypes.IsMatch(WithoutStrings(line.Text)) &&
                               !(line.File == "WeaponAsset.cs" && line.Text.Contains("public AnimatorOverrideController Motions")) &&
                               !(line.File == "AICharacter.Legacy.cs" && line.Text.Contains("public Component Animator;")))
                .Select(line => $"{line.File}:{line.Line}: {line.Text}").ToList();

            CollectionAssert.IsEmpty(found, "The core shows nothing: the AI Animator (AIKit.Animation) reads the model");
        }

        [Test]
        public void TheCore_ItsTypes_HoldNoAnimatorClipOrController_ButTheWeaponsClipSet()
        {
            var found = new List<string>();
            const System.Reflection.BindingFlags ALL = System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.Static |
                                                       System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic |
                                                       System.Reflection.BindingFlags.DeclaredOnly;
            foreach (System.Type type in typeof(AICharacter).Assembly.GetTypes())
            {
                foreach (System.Reflection.FieldInfo field in type.GetFields(ALL))
                {
                    if (IsAnimationType(field.FieldType) && !(type == typeof(WeaponAsset) && field.Name == "Motions"))
                        found.Add($"{type.Name}.{field.Name}");
                }

                foreach (System.Reflection.MethodInfo method in type.GetMethods(ALL))
                {
                    if (IsAnimationType(method.ReturnType) ||
                        method.GetParameters().Any(parameter => IsAnimationType(parameter.ParameterType)))
                        found.Add($"{type.Name}.{method.Name}()");
                }
            }

            CollectionAssert.IsEmpty(found, "No core type holds or passes an animator, a clip or a controller");
        }

        private static bool IsAnimationType(System.Type type)
        {
            while (type != null && (type.IsArray || type.IsByRef))
                type = type.GetElementType();
            if (type is { IsGenericType: true })
                return type.GetGenericArguments().Any(IsAnimationType);

            return type != null && (typeof(UnityEngine.Animator).IsAssignableFrom(type) ||
                                    typeof(UnityEngine.Motion).IsAssignableFrom(type) ||
                                    typeof(UnityEngine.RuntimeAnimatorController).IsAssignableFrom(type));
        }

        /// <summary>
        /// The core files allowed to tell a presenter what to show: none (step 90, C22 — the transitional IActionAnimator is
        /// gone; every node and runner asks AgentContext.Actions).
        /// </summary>
        private static readonly string[] StillTellingThePresenter = System.Array.Empty<string>();

        [Test]
        public void TheCore_NoNodeOrRunnerTellsThePresentation_ItAsksTheCharactersActions()
        {
            // The model publishes what the character does (ActionState); only ActionState asks the view its two questions
            List<string> telling = Find(Code("Runtime/Core"), new[] { "ActionAnimator", "IActionAnimator" },
                StillTellingThePresenter);
            List<string> asking = Find(Code("Runtime/Core"), new[] { "View.Has(", "View.IsShowing(", "IActionView " },
                "ActionState.cs");

            CollectionAssert.IsEmpty(telling, "nodes and runners ask context.Actions (Begin / Swing / End), never a presenter");
            CollectionAssert.IsEmpty(asking, "only ActionState puts the view's questions");
        }
    }
}
