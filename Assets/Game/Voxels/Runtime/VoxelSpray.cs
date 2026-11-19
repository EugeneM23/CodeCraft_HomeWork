using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>A fan of drops (VoxelDebris.Drop): blood out of a wound or a torn seam.</summary>
    public static class VoxelSpray
    {
        /// <param name="spread">How far a drop strays from <paramref name="direction"/>: 0 — none, 1 — a wide cone.</param>
        /// <param name="inherit">Velocity of what bleeds, added to every drop.</param>
        /// <returns>How many drops flew (fewer when the debris store is full).</returns>
        public static int Burst(Vector3 at, Vector3 direction, int count, float minSpeed, float maxSpeed, float spread,
            IReadOnlyList<Color32> colors, float size, Vector3 inherit = default)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null || colors == null || colors.Count == 0 || count <= 0)
                return 0;

            Vector3 aim = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 heading = (aim + Random.insideUnitSphere * spread).normalized;
                Vector3 velocity = inherit + heading * Random.Range(minSpeed, maxSpeed);
                Color32 color = colors[Random.Range(0, colors.Count)];
                if (debris.Drop(at, velocity, color, size * Random.Range(0.8f, 1.2f)) < 0)
                    break;
                spawned++;
            }

            return spawned;
        }

        /// <summary>
        /// A splash of sticky goo out of a body that burst: strands (drops thrown one way, fast to slow, so they string
        /// out as they rise) and a few blobs (clumps that hold together). It all lifts sluggishly, hangs at the top,
        /// drops heavily and sticks where it lands (VoxelDebris.GooDrop, SetGoo): no pea-like scatter.
        /// </summary>
        /// <param name="drops">Single drops in all; strands of about five, plus a blob per eight.</param>
        /// <param name="speed">Launch speed of a strand's fastest drop, m/s.</param>
        /// <param name="spread">How far strands stray from <paramref name="direction"/>: 0 — none, 1 — a wide cone.</param>
        /// <param name="radius">How far round <paramref name="at"/> they start (the body's size).</param>
        /// <returns>Drops and blobs that flew.</returns>
        public static int Goo(Vector3 at, Vector3 direction, int drops, float speed, float spread,
            IReadOnlyList<Color32> colors, float size, Vector3 inherit = default, float radius = 0.2f)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null || colors == null || colors.Count == 0 || drops <= 0)
                return 0;

            Vector3 aim = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            int strands = Mathf.Max(1, drops / 5);
            int perStrand = Mathf.Max(1, drops / strands);
            int spawned = 0;
            for (int s = 0; s < strands; s++)
            {
                Vector3 heading = (aim + Random.insideUnitSphere * spread).normalized;
                float top = speed * Random.Range(0.75f, 1.1f);
                Vector3 start = at + Random.insideUnitSphere * radius;
                Color32 color = colors[Random.Range(0, colors.Count)];
                for (int k = 0; k < perStrand; k++)
                {
                    // Along the strand the drops slow down: they rise to different heights and string out between.
                    float t = perStrand > 1 ? k / (perStrand - 1f) : 0f;
                    Vector3 velocity = inherit + heading * (top * Mathf.Lerp(1f, 0.4f, t));
                    Color32 c = Random.value < 0.3f ? colors[Random.Range(0, colors.Count)] : color;
                    if (debris.GooDrop(start + heading * (0.03f * k), velocity, c, size * Random.Range(0.8f, 1.3f)) < 0)
                        return spawned;
                    spawned++;
                }
            }

            int blobs = Mathf.Max(1, drops / 8);
            for (int b = 0; b < blobs; b++)
            {
                Vector3 heading = (aim + Random.insideUnitSphere * spread).normalized;
                Vector3 centre = at + Random.insideUnitSphere * radius;
                int cubes = Random.Range(4, 8);
                if (!debris.HasGoreRoom(cubes))
                    break;
                int cluster = debris.BeginGoreCluster(centre, inherit + heading * (speed * Random.Range(0.55f, 0.85f)), 1f);
                if (cluster < 0)
                    break;
                for (int k = 0; k < cubes; k++)
                    debris.AddCube(cluster, centre + Random.insideUnitSphere * (size * 1.1f), size * Random.Range(0.9f, 1.3f),
                        colors[Random.Range(0, colors.Count)]);
                debris.SetGoo(cluster);
                spawned++;
            }

            return spawned;
        }
    }
}
