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

        /// <summary>One body part of <see cref="Chunks"/>: its size in cubes and where its middle sits (share of the body's height, sideways in cubes).</summary>
        private static readonly (int x, int y, int z, float height, float side)[] Parts =
        {
            (3, 3, 3, 0.9f, 0f),   // head
            (4, 6, 3, 0.6f, 0f),   // torso
            (2, 5, 2, 0.62f, -3.5f), // arms
            (2, 5, 2, 0.62f, 3.5f),
            (2, 6, 2, 0.22f, -1.2f), // legs
            (2, 6, 2, 0.22f, 1.2f)
        };

        /// <summary>
        /// A body torn apart by a shell (the user: bigger pieces): head, torso, two arms and two legs as clumps of cubes of
        /// <paramref name="height"/>/24 each, skin outside and meat within, thrown up and out from the body like the
        /// sticky goo (VoxelDebris.SetGoo): a sluggish rise, a heavy drop, and they lie whole where they land. About 190
        /// cubes of the gore share of the store.
        /// </summary>
        /// <param name="at">The feet of the body.</param>
        /// <param name="away">Which way the hit threw it (horizontal; zero: no way in particular).</param>
        /// <returns>Chunks thrown.</returns>
        public static int Chunks(Vector3 at, float height, Vector3 away, IReadOnlyList<Color32> skin, IReadOnlyList<Color32> meat)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null || skin == null || skin.Count == 0 || meat == null || meat.Count == 0 || height <= 0f)
                return 0;

            float cube = height / 24f;
            Vector3 side = Vector3.Cross(Vector3.up, away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward);
            int thrown = 0;
            for (int p = 0; p < Parts.Length; p++)
            {
                (int x, int y, int z, float h, float s) = Parts[p];
                if (!debris.HasGoreRoom(x * y * z))
                    break;
                Vector3 middle = at + Vector3.up * (height * h) + side * (s * cube);
                Vector3 out_ = (middle - (at + Vector3.up * height * 0.6f));
                out_.y = 0f;
                Vector3 velocity = Vector3.up * Random.Range(4f, 6.5f) + out_.normalized * Random.Range(1f, 2.5f) +
                                   away * Random.Range(1f, 2f) + Random.insideUnitSphere * 0.6f;
                int cluster = debris.BeginGoreCluster(middle, velocity, 3f);
                if (cluster < 0)
                    break;
                for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                for (int k = 0; k < z; k++)
                {
                    bool surface = i == 0 || j == 0 || k == 0 || i == x - 1 || j == y - 1 || k == z - 1;
                    Color32 colour = surface && Random.value > 0.2f ? skin[Random.Range(0, skin.Count)] : meat[Random.Range(0, meat.Count)];
                    Vector3 offset = new Vector3(i - (x - 1) * 0.5f, j - (y - 1) * 0.5f, k - (z - 1) * 0.5f) * cube;
                    debris.AddCube(cluster, middle + offset, cube, colour);
                }

                debris.SetGoo(cluster);
                thrown++;
            }

            return thrown;
        }
    }
}
