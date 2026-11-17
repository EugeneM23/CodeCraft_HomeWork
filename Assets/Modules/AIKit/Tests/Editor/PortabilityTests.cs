using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 10: the module moves to another project as a folder. No assembly of the module refers to the game, and the
    /// core refers to nothing at all — read from the module's own asmdef files (found from the module's folder, not by a
    /// path typed here).
    /// </summary>
    public class PortabilityTests
    {
        private static string ModuleFolder()
        {
            string[] guids = AssetDatabase.FindAssets("AIKit t:AssemblyDefinitionAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "AIKit.asmdef")
                    return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))?.Replace('\\', '/');
            }

            Assert.Fail("AIKit.asmdef not found");
            return null;
        }

        [System.Serializable]
        private sealed class AsmDef
        {
            public string name;
            public string[] references;
        }

        private static string NameOf(string reference)
        {
            if (!reference.StartsWith("GUID:"))
                return reference;

            string path = AssetDatabase.GUIDToAssetPath(reference.Substring(5));
            return Path.GetFileNameWithoutExtension(path);
        }

        private static List<AsmDef> ModuleAssemblies()
        {
            var result = new List<AsmDef>();
            foreach (string file in Directory.GetFiles(ModuleFolder(), "*.asmdef", SearchOption.AllDirectories))
                result.Add(JsonUtility.FromJson<AsmDef>(File.ReadAllText(file)));

            return result;
        }

        [Test]
        public void NoModuleAssembly_RefersToTheGame()
        {
            List<AsmDef> assemblies = ModuleAssemblies();
            Assert.Greater(assemblies.Count, 5, "The module's assemblies were found");

            foreach (AsmDef assembly in assemblies)
            {
                foreach (string reference in assembly.references ?? new string[0])
                {
                    string name = NameOf(reference);
                    Assert.IsFalse(name == "Game" || name.StartsWith("Game."),
                        $"{assembly.name} refers to the game's assembly {name}");
                }
            }
        }

        [Test]
        public void TheCore_RefersToNothing()
        {
            AsmDef core = ModuleAssemblies().Find(assembly => assembly.name == "AIKit");

            CollectionAssert.IsEmpty(core.references ?? new string[0], "The core needs no other assembly");
        }

        [Test]
        public void TheAnimationAdapter_RefersOnlyToTheCore_NothingOfTheModuleRefersToIt()
        {
            // Step 90 (C22): the picture is an adapter of its own (AI Animator, Mecanim) — the model knows nothing of it;
            // a game with another animation system leaves it out
            List<AsmDef> assemblies = ModuleAssemblies();
            AsmDef animation = assemblies.Find(assembly => assembly.name == "AIKit.Animation");
            Assert.IsNotNull(animation, "AIKit.Animation is one of the module's assemblies");
            CollectionAssert.AreEqual(new[] { "AIKit" }, System.Array.ConvertAll(animation.references ?? new string[0], NameOf),
                "the animation adapter refers to the core only");

            foreach (AsmDef assembly in assemblies)
            {
                if (assembly.name is "AIKit.Animation.Editor" || assembly.name.Contains("Tests"))
                    continue;

                foreach (string reference in assembly.references ?? new string[0])
                {
                    Assert.AreNotEqual("AIKit.Animation", NameOf(reference),
                        $"{assembly.name} refers to the animation adapter: the model must not know its picture");
                }
            }
        }
    }
}
