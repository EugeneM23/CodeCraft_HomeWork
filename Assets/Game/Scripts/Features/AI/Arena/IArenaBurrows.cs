using System.Collections.Generic;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The arena as the director uses it: bounds and burrows. In the game the Arenas team's
    /// <c>IArenaService</c> (<see cref="ArenaServiceBurrows"/>); <see cref="EdgeBurrowsLayout"/> in the sandbox, tests and
    /// the bench. The director picks the burrow itself (the farthest suitable one) and opens it by index.
    /// </summary>
    public interface IArenaBurrows
    {
        /// <summary>The playable rectangle (world space; arenas stand anywhere in the world).</summary>
        Bounds Bounds { get; }

        /// <summary>Burrow points on the ground.</summary>
        IReadOnlyList<Vector3> BurrowPoints { get; }

        /// <summary>Tears the ground open at burrow <paramref name="index"/> (once) and returns where the horde comes out.</summary>
        Vector3 OpenBurrow(int index);
    }
}
