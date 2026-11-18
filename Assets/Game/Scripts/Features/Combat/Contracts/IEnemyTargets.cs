using UnityEngine;

namespace Game
{
    /// <summary>
    /// All enemies the guns, turrets and skills can aim at, over every registered source. GameContext key:
    /// EnemyTargets. The AI team registers the horde here.
    /// </summary>
    public interface IEnemyTargets
    {
        /// <summary>Adds a source; returns its id for <see cref="Unregister"/>.</summary>
        int Register(IEnemyTargetSource source);

        void Unregister(int source);

        int AliveCount { get; }

        bool TryFindNearest(Vector3 from, float range, out EnemyRef target, out Vector3 position);

        /// <summary>The point within range with the most enemies within clusterRadius of it.</summary>
        bool TryFindDensest(Vector3 from, float range, float clusterRadius, out Vector3 point);

        bool IsAlive(in EnemyRef target);

        Vector3 GetPosition(in EnemyRef target);

        bool Raycast(Vector3 from, Vector3 to, float radius, out EnemyRef target, out Vector3 point);

        void Damage(in EnemyRef target, float damage, Vector3 hitPoint);

        void DamageArea(Vector3 center, float radius, float damage, float minFactor);
    }
}
