using UnityEngine;

namespace Game
{
    /// <summary>
    /// A target source whose enemies can be torn apart by a shell's direct hit (the horde: big chunks instead of a whole
    /// corpse). Optional: EnemyTargets.Tear asks only the sources that implement it.
    /// </summary>
    public interface IEnemyTearable
    {
        /// <summary>The enemy at the slot, if the damage queued now kills it, is torn apart.</summary>
        void Tear(int index, int version, Vector3 direction);
    }
}
