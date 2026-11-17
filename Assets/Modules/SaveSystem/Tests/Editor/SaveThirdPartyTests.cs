using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// Third-party code (plugins, frameworks, packages) is not the game's scripts: its components are left to
    /// themselves like Unity's own, and a game script that derives from one keeps only what it declares itself.
    /// </summary>
    public class SaveThirdPartyTests
    {
        /// <summary>A script of the game built on a framework class that lives in another assembly.</summary>
        private sealed class OwnProbe : ThirdPartyBaseProbe
        {
            public int Own = 2;

#pragma warning disable CS0414 // read through reflection only
            private float _angle = 3f;
#pragma warning restore CS0414
        }

        private string[] _before;

        private static string FrameworkAssembly => typeof(ThirdPartyBaseProbe).Assembly.GetName().Name;

        [SetUp]
        public void SetUp() => _before = SaveMembers.ThirdPartyAssemblies.ToArray();

        [TearDown]
        public void TearDown() => SaveMembers.SetThirdPartyAssemblies(_before);

        private static string[] Names(Type type) =>
            SaveMembers.AllWithPrivate(type).Select(member => member.Name).ToArray();

        [Test]
        public void ThirdParty_NotListed_IsAScriptLikeAnyOther()
        {
            SaveMembers.SetThirdPartyAssemblies(Array.Empty<string>());

            Assert.IsFalse(SaveMembers.IsThirdParty(typeof(ThirdPartyBaseProbe)));
            Assert.IsFalse(SaveMembers.UsesProperties(typeof(ThirdPartyBaseProbe)));
            CollectionAssert.IsSubsetOf(new[] { "BaseShown", "_hasRun" }, Names(typeof(ThirdPartyBaseProbe)));
            CollectionAssert.IsSubsetOf(new[] { "Own", "_angle", "BaseShown", "_hasRun" }, Names(typeof(OwnProbe)));
        }

        [Test]
        public void ThirdParty_ItsComponent_FieldsAreLeftAlone()
        {
            SaveMembers.SetThirdPartyAssemblies(new[] { FrameworkAssembly });

            Assert.IsTrue(SaveMembers.IsThirdParty(typeof(ThirdPartyBaseProbe)));
            Assert.IsTrue(SaveMembers.UsesProperties(typeof(ThirdPartyBaseProbe)), "saved like Unity's own components");
            string[] names = Names(typeof(ThirdPartyBaseProbe));
            CollectionAssert.DoesNotContain(names, "BaseShown");
            CollectionAssert.DoesNotContain(names, "_hasRun");
        }

        [Test]
        public void ThirdParty_BaseOfAGameScript_OnlyTheGamesOwnMembers()
        {
            SaveMembers.SetThirdPartyAssemblies(new[] { FrameworkAssembly });

            Assert.IsFalse(SaveMembers.IsThirdParty(typeof(OwnProbe)));
            Assert.IsFalse(SaveMembers.UsesProperties(typeof(OwnProbe)), "still a script of the game");
            string[] names = Names(typeof(OwnProbe));
            CollectionAssert.IsSubsetOf(new[] { "Own", "_angle" }, names);
            CollectionAssert.DoesNotContain(names, "BaseShown");
            CollectionAssert.DoesNotContain(names, "_hasRun", "what the framework keeps to itself is not the script's state");
        }

        [Test]
        public void ThirdParty_WholeScene_DoesNotTakeItsComponent()
        {
            var target = new GameObject("Framework object");
            try
            {
                ThirdPartyBaseProbe component = target.AddComponent<ThirdPartyBaseProbe>();
                ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
                SaveMembers.SetThirdPartyAssemblies(Array.Empty<string>());
                Assert.AreEqual(SaveCoverage.Scripts, SavePolicy.CategoryOf(component, catalog));

                SaveMembers.SetThirdPartyAssemblies(new[] { FrameworkAssembly });

                Assert.AreEqual(SaveCoverage.None, SavePolicy.CategoryOf(component, catalog));
                Assert.IsFalse(SavePolicy.HasState(target, catalog), "an object with framework components only has no state of the game");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ThirdParty_ListChanges_TypesAreLookedAtAgain()
        {
            SaveMembers.SetThirdPartyAssemblies(Array.Empty<string>());
            CollectionAssert.Contains(Names(typeof(OwnProbe)), "_hasRun");

            SaveMembers.SetThirdPartyAssemblies(new[] { FrameworkAssembly });

            CollectionAssert.DoesNotContain(Names(typeof(OwnProbe)), "_hasRun", "the answer of before is not kept");
        }

        [Test]
        public void Catalog_ThirdPartyAssemblies_WithoutUnityAndTheModule()
        {
            List<string> names = SaveCatalogBuilder.BuildThirdPartyAssemblies();

            CollectionAssert.AllItemsAreUnique(names);
            CollectionAssert.IsOrdered(names, StringComparer.Ordinal);
            Assert.IsFalse(names.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)
                                             || name.StartsWith("Unity.", StringComparison.Ordinal)), "Unity's own are known without a list");
            Assert.IsFalse(names.Any(name => name == "SaveSystem" || name.StartsWith("SaveSystem.", StringComparison.Ordinal)),
                "the module's demo and tests behave the same wherever the module is put");
            CollectionAssert.DoesNotContain(names, "Assembly-CSharp", "scripts in Assets outside Plugins are the game's");
        }

        [Test]
        public void Settings_ThirdPartyAssemblies_ReachTheMembers()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            try
            {
                settings.SetThirdPartyAssemblies(new[] { FrameworkAssembly });

                CollectionAssert.AreEqual(new[] { FrameworkAssembly }, settings.ThirdPartyAssemblies);
                Assert.IsTrue(SaveMembers.IsThirdParty(typeof(ThirdPartyBaseProbe)));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
