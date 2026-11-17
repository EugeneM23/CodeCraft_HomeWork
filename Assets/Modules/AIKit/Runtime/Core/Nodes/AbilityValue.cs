using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// An ability as a node of data (step 90): one ability asset, wired into the Abilities pin of Use Any Ability — the
    /// decision's set is the wires that come in, as Detection's shapes are (the user turned down lists on a node twice).
    /// It only names the ability; the character prepares its runner.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Ability", "Value", "aikit.value.ability", Simple = true)]
    public sealed class AbilityValue : AgentValue, IUsesAbilities
    {
        /// <summary>The ability (a project asset, kept in the graph asset's table of objects).</summary>
        [NonSerialized] public AbilityAsset Ability;

        public AbilityValue()
        {
        }

        public AbilityValue(AbilityAsset ability) => Ability = ability;

        public override ValueKind Kind => ValueKind.Ability;

        public void CollectAbilities(List<AbilityAsset> abilities)
        {
            if (Ability != null)
                abilities.Add(Ability);
        }
    }
}
