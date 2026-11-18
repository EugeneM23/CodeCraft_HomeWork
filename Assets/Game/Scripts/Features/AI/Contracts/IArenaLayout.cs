using System.Collections.Generic;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The arena as the AI needs it: its bounds and its burrows (spawn points on the edge). Given by the Arenas team's
    /// generator; a stand-in until their contract is agreed. Burrow points are on the ground, world space.
    /// </summary>
    public interface IArenaLayout
    {
        Bounds Bounds { get; }

        IReadOnlyList<Vector3> Burrows { get; }
    }
}
