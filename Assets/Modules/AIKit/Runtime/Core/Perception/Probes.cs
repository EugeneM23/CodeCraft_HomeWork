using UnityEngine;

namespace AIKit
{
    // The questions perception asks the physics of the world. Declared in the core, which stays without physics and
    // dimensions (Vector3 plus the body's Up); implemented only in adapters (2D physics, 3D physics, a grid), so there
    // is one sensor and never two copies of it (invariant 8). The agent finds them on its entity like the actuator.

    /// <summary>Line of sight: nothing that blocks sight between two points.</summary>
    public interface ISightProbe
    {
        bool HasLineOfSight(Vector3 from, Vector3 to);
    }

    /// <summary>Ground under a point: edges, pits and landing spots.</summary>
    public interface IGroundProbe
    {
        /// <summary>Looks down (against up) from the point at most this far; true — ground found, with where.</summary>
        bool FindGround(Vector3 point, Vector3 up, float maxDepth, out Vector3 ground);
    }

    /// <summary>
    /// "An ally is in front of me": the legal stand-in until the roster of stage 8 (invariant 1 — an agent does not
    /// read another agent — is closed by the roster; until then this probe is an accepted debt, used by stage 5).
    /// </summary>
    public interface IAllyProbe
    {
        bool AllyAhead(Vector3 from, Vector3 direction, float distance);
    }
}
