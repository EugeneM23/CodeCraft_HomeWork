using UnityEngine;

namespace Game
{
    /// <summary>
    /// Optional for a target source: the nearest live enemy farther than a distance and within a range (the
    /// ricochet looks outside its own blast). A source without it is asked for its plain nearest, which counts
    /// only when it lies beyond the distance.
    /// </summary>
    public interface IEnemyTargetRingQuery
    {
        bool TryFindNearestBeyond(Vector3 from, float minRange, float maxRange, out int index, out int version,
            out Vector3 position);
    }
}
