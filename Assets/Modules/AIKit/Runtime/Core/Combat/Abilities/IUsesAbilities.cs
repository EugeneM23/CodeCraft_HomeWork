using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// A node of the graph that names abilities (step 90, C23: the abilities are the graph's, not a list on the character):
    /// the Use Ability decision and block, the Ability value of Use Any Ability's set. The character prepares its own
    /// runner for every ability its graph names (AICharacter.SetGraph).
    /// </summary>
    public interface IUsesAbilities
    {
        /// <summary>Adds the abilities the node names (none is skipped).</summary>
        void CollectAbilities(List<AbilityAsset> abilities);
    }
}
