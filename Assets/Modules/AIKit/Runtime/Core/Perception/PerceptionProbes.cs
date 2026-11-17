using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// "Who is around": the candidates a perception may look at, within a radius of a point. Implemented by adapters
    /// (an overlap on the target layers); the agent's own entity never comes back. The core never asks the physics.
    /// </summary>
    public interface ITargetScanner
    {
        /// <summary>Fills results (cleared first) with the root transforms of candidates; returns their count.</summary>
        int Scan(Vector3 center, float radius, List<Transform> results);
    }

    /// <summary>
    /// A target's own say on being seen (optional on the target): dead or pooled ones are not perceivable, a faction
    /// tells friend from foe, the aim point is where eyes look at (the chest, not the feet).
    /// </summary>
    public interface IPerceivable
    {
        bool IsPerceivable { get; }

        int Faction { get; }

        Vector3 AimPoint { get; }
    }

    /// <summary>
    /// Where the eyes look at a target that does not say it itself (no IPerceivable): an adapter knows the body — the
    /// centre of its collider, not its feet (a ray to the feet grazes the floor).
    /// </summary>
    public interface IAimProbe
    {
        Vector3 AimPoint(Transform target);
    }

    /// <summary>The agent's game-side say on what counts as a target (optional, on the agent's entity): "only the living".</summary>
    public interface ITargetFilter
    {
        bool Accepts(Transform candidate);
    }
}
