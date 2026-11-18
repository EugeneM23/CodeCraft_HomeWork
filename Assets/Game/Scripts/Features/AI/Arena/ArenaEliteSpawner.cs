using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Elites from a prefab (<c>Regax_Elite.prefab</c>) through <see cref="EliteRegaxSpawn"/>. The strength is kept in
    /// <see cref="LastStrength"/> for the elite's own health scaling (the elite team's).
    /// </summary>
    public sealed class ArenaEliteSpawner : IArenaEliteSpawner
    {
        private readonly GameObject _prefab;

        public ArenaEliteSpawner(GameObject prefab) => _prefab = prefab;

        public float LastStrength { get; private set; } = 1f;

        public GameObject LastElite { get; private set; }

        public bool SpawnElite(Vector3 at, Vector3 facing, float strength)
        {
            if (_prefab == null)
                return false;
            LastStrength = strength;
            LastElite = EliteRegaxSpawn.Spawn(_prefab, at, facing); // perf-ok: one elite per arena, behind its burrow
            return LastElite != null;
        }
    }
}
