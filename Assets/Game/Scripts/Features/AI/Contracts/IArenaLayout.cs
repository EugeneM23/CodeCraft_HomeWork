using System.Collections.Generic;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The arena as the AI needs it, a stand-in with the same members as the Arenas team's <c>IArenaService</c>
    /// (AppContext): replaced by it once its contract is published. Burrow points are on the ground, world space.
    /// </summary>
    public interface IArenaLayout
    {
        /// <summary>The arena's rectangle.</summary>
        Bounds Bounds { get; }

        /// <summary>Every burrow of the arena (spawn points on its edge).</summary>
        IReadOnlyList<Vector3> BurrowPoints { get; }

        /// <summary>Opens a burrow at least 15 m from the tank (the voxel crater is the arena's): false if none fits.</summary>
        bool TryOpenBurrow(Vector3 tankPosition, out Vector3 point);

        /// <summary>Opens the burrow at this point (crater included), whatever the tank's distance.</summary>
        void OpenBurrowAt(Vector3 point);
    }
}
