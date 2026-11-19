using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A high-explosive shell or an air bomb going off near a house blows its windows out (user, 2026-11-20): every
    /// blast in the level (ExplosionEvents) of at least <see cref="MIN_BLAST"/> m shatters the glass of the houses
    /// round it (VoxelDamage.ShatterGlass) — all of it within <see cref="SURE"/> of the reach, fewer panes further
    /// off, none past <see cref="REACH"/> times the blast's radius (<see cref="MIN_REACH"/> at least). An armour-piercing
    /// shell's small blast does not.
    /// </summary>
    public static class HouseGlassBreaker
    {
        /// <summary>Smallest blast (m) that breaks windows: an HE shell's 5 m and a bomb's 3.4–4 m do, an AP shell's 1.25 m does not.</summary>
        public const float MIN_BLAST = 2.5f;

        /// <summary>Glass breaks out to this many times the blast's radius (7 m round an HE shell)…</summary>
        public const float REACH = 1.4f;

        /// <summary>…and at least this far (m): an air bomb's blast is told as 3.4–4 m, but it is the bigger charge.</summary>
        public const float MIN_REACH = 7f;

        /// <summary>Within this share of the reach every pane goes; past it fewer and fewer.</summary>
        public const float SURE = 0.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            ExplosionEvents.Exploded -= OnExploded;
            ExplosionEvents.Exploded += OnExploded;
        }

        /// <summary>The windows a blast of <paramref name="radius"/> m at <paramref name="point"/> breaks.</summary>
        public static int Break(Vector3 point, float radius)
        {
            if (radius < MIN_BLAST)
                return 0;
            float reach = Mathf.Max(MIN_REACH, radius * REACH);
            return VoxelDamage.ShatterGlass(point, reach * SURE, reach);
        }

        private static void OnExploded(Vector3 point, float radius) => Break(point, radius);
    }
}
