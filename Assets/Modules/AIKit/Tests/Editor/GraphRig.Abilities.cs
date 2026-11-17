using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>The rig's abilities as the graph names them (step 90): ability assets made in the test.</summary>
    internal sealed partial class GraphRig
    {
        private static readonly Dictionary<string, AbilityAsset> NamedAbilities = new();
        private static readonly List<AbilityAsset> MadeAbilities = new();

        /// <summary>
        /// The ability a test runner of this id plays (AddAbility): an asset that leaves the playing to the runner registered
        /// under its name (a Code Runner delivery) — what a graph names. One per id for the whole run.
        /// </summary>
        public static AbilityAsset Named(string id)
        {
            if (NamedAbilities.TryGetValue(id, out AbilityAsset ability) && ability != null)
                return ability;

            ability = AbilityAsset.CodeRunner(id);
            ability.hideFlags = HideFlags.HideAndDontSave;
            NamedAbilities[id] = ability;
            return ability;
        }

        /// <summary>An ability made of these parts (destroyed after the run).</summary>
        public static AbilityAsset MakeAbility(string name, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            ability.hideFlags = HideFlags.HideAndDontSave;
            MadeAbilities.Add(ability);
            return ability;
        }

        /// <summary>The assets the rigs made for the abilities go.</summary>
        public static void DestroyAbilities()
        {
            foreach (AbilityAsset ability in NamedAbilities.Values)
            {
                if (ability != null)
                    Object.DestroyImmediate(ability);
            }

            NamedAbilities.Clear();
            foreach (AbilityAsset ability in MadeAbilities)
            {
                if (ability != null)
                    Object.DestroyImmediate(ability);
            }

            MadeAbilities.Clear();
        }
    }

    /// <summary>After every test of the assembly: the ability assets the rigs made are destroyed.</summary>
    [SetUpFixture]
    public sealed class GraphRigAbilitiesCleanup
    {
        [OneTimeTearDown]
        public void DestroyAbilities() => GraphRig.DestroyAbilities();
    }
}
