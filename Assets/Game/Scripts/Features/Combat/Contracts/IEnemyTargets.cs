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

        /// <summary>The target, if damage queued now kills it, is torn apart (a shell's direct hit) rather than falling whole.</summary>
        /// <param name="direction">Which way the shell flew: the body's goo and pieces are thrown on that way (zero: none).</param>
        /// <param name="power">How hard the shell throws them: the «Мощный фугас» skill (ShellPowerUseCase), 1 — as without it.</param>
        void Tear(in EnemyRef target, Vector3 direction, float power = 1f);

        void DamageArea(Vector3 center, float radius, float damage, float minFactor);
    }
}
