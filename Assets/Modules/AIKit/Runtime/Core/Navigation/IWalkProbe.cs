using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// "How far can the body go straight from here": the ground the body may stand on, asked along a line (a NavMesh
    /// raycast, a grid walk). A node asks it before it steps: Kite looks whether the way back is free, Charge how far it
    /// can run before a wall. Declared in the core, answered by the body (the core never asks the physics); a body without
    /// one — every way is free.
    /// </summary>
    public interface IWalkProbe
    {
        /// <summary>
        /// Metres the body can walk from <paramref name="from"/> along <paramref name="direction"/> (flat) before the
        /// ground it may stand on ends — a wall, an edge — at most <paramref name="maxDistance"/>.
        /// </summary>
        float FreeDistance(Vector3 from, Vector3 direction, float maxDistance);
    }
}
