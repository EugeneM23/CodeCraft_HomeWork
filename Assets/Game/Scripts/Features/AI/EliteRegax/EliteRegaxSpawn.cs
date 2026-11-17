using Atomic.Entities;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Puts elites into a running level from code (tests, the demo script, a level without them in its scene): the
    /// level's AI entity is made if missing, the elite stands on the grid's ground and faces the given way.
    /// </summary>
    public static class EliteRegaxSpawn
    {
        /// <summary>An elite from <paramref name="prefab"/> on the ground at <paramref name="position"/> (XZ).</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Vector3 facing)
        {
            HordeNavGrid grid = LevelAI.Ensure().GetLevelNavigation().Grid;
            float height = grid.HeightAt(position);
            if (!float.IsNaN(height))
                position.y = height;

            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            GameObject elite = Object.Instantiate(prefab, position, rotation); // perf-ok: demo and test spawns, not a battle pool
            elite.name = prefab.name;
            return elite;
        }

        /// <summary>
        /// A point on walkable ground about <paramref name="distance"/> m from <paramref name="center"/> in the direction
        /// <paramref name="direction"/> (turned a little either way until the grid has low ground there); the point as
        /// asked when nothing fits.
        /// </summary>
        public static Vector3 GroundNear(Vector3 center, Vector3 direction, float distance, float maxHeightAbove = 1f)
        {
            HordeNavGrid grid = LevelAI.Ensure().GetLevelNavigation().Grid;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            float baseHeight = grid.HeightAt(center);
            for (int i = 0; i < 24; i++)
            {
                float angle = (i % 2 == 0 ? 1f : -1f) * (i / 2) * 7.5f;
                Vector3 point = center + Quaternion.AngleAxis(angle, Vector3.up) * direction * distance;
                float h = grid.HeightAt(point);
                if (float.IsNaN(h) || (!float.IsNaN(baseHeight) && h > baseHeight + maxHeightAbove))
                    continue;
                point.y = h;
                return point;
            }

            return center + direction * distance;
        }
    }
}
