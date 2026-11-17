using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace AIKit.Tests
{
    /// <summary>
    /// Invariant 3 (time comes only as dt) in the adapters too (whole-plugin recheck, brain): what a decision asks of
    /// the body — the Climb decision's "on foot or only by climbing?" (IClimbingNavigator.WayTo) — runs on the agent's
    /// own clock, never on the engine's, so a decision replays in a test with manual steps. Only the components that
    /// drive themselves frame by frame (a projectile's flight, the animator's picture) read the engine's frame time.
    /// </summary>
    public sealed class BrainInvariantTests
    {
        [Test]
        public void InvariantTests_Invariant3_NoEngineTime_InTheAdaptersEither()
        {
            List<string> found = Find(Code("Adapters"),
                new[] { "Time.time", "Time.deltaTime", "Time.fixedDeltaTime", "Time.unscaled", "StartCoroutine",
                    "Invoke(\"", "WaitForSeconds" },
                "ArcFlight.cs", "ArrowFlight.cs", "AIAnimator.cs");     // they drive themselves frame by frame

            CollectionAssert.IsEmpty(found, "Time comes only as dt — what a decision asks of the body too");
        }

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

        // The code lines (comments dropped) of the module's sources under a folder
        private static IEnumerable<(string File, int Line, string Text)> Code(string folder)
        {
            string root = Path.Combine(ModuleFolder(), folder);
            Assert.IsTrue(Directory.Exists(root), root);
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string text = lines[i].Trim();
                    if (!text.StartsWith("//"))
                        yield return (Path.GetFileName(file), i + 1, text);
                }
            }
        }

        private static List<string> Find(IEnumerable<(string File, int Line, string Text)> code, string[] patterns,
            params string[] allowedFiles) =>
            code.Where(line => patterns.Any(line.Text.Contains) && !allowedFiles.Contains(line.File))
                .Select(line => $"{line.File}:{line.Line}: {line.Text}").ToList();
    }
}
