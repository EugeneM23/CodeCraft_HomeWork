using UnityEngine;

namespace Game
{
    /// <summary>
    /// A set of enemies combat can find and hurt: entity enemies (combat's own source) or the horde (the AI team's).
    /// Registered in <see cref="IEnemyTargets"/>. Nothing here may allocate: it runs in ticks.
    /// </summary>
    public interface IEnemyTargetSource
    {
        int AliveCount { get; }

        /// <summary>The nearest live enemy within range of the point.</summary>
        bool TryFindNearest(Vector3 from, float range, out int index, out int version, out Vector3 position);

        bool IsAlive(int index, int version);

        /// <summary>Aim point (centre of the body).</summary>
        Vector3 GetPosition(int index);

        /// <summary>First enemy a ball of this radius meets going from → to.</summary>
        bool Raycast(Vector3 from, Vector3 to, float radius, out int index, out int version, out Vector3 point);

        void Damage(int index, int version, float damage, Vector3 hitPoint);

        /// <summary>Area damage, falling off linearly to minFactor at the edge.</summary>
        void DamageArea(Vector3 center, float radius, float damage, float minFactor);

        /// <summary>Live enemies within radius of the point.</summary>
        int CountInRadius(Vector3 center, float radius);
    }
}
