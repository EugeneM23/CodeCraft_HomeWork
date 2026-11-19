using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>A fan of drops (VoxelDebris.Drop): blood out of a wound or a torn seam.</summary>
    public static class VoxelSpray
    {
        /// <summary>
        /// Up-speed of everything gore throws (drops, goo, body parts) is this many times what the callers ask: gore flies
        /// 20 % higher (the user), as the height of a throw goes with the square of its up-speed.
        /// </summary>
        public const float LIFT = 1.0954451f;

        /// <summary>Body parts (<see cref="Chunks"/>) and the blobs of goo are this many times bigger (the user: 25 %).</summary>
        public const float PIECE_SCALE = 1.25f;

        /// <summary>A gore velocity with its up-speed raised by <see cref="LIFT"/> (a downward one is left as it is).</summary>
        public static Vector3 Lifted(Vector3 velocity)
        {
            if (velocity.y > 0f)
                velocity.y *= LIFT;
            return velocity;
        }

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
                if (debris.Drop(at, Lifted(velocity), color, size * Random.Range(0.8f, 1.2f)) < 0)
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
                    if (debris.GooDrop(start + heading * (0.03f * k), Lifted(velocity), c, size * Random.Range(0.8f, 1.3f)) < 0)
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
                int cluster = debris.BeginGoreCluster(centre, Lifted(inherit + heading * (speed * Random.Range(0.55f, 0.85f))), 1f);
                if (cluster < 0)
                    break;
                for (int k = 0; k < cubes; k++)
                    debris.AddCube(cluster, centre + Random.insideUnitSphere * (size * 1.1f * PIECE_SCALE), size * PIECE_SCALE * Random.Range(0.9f, 1.3f),
                        colors[Random.Range(0, colors.Count)]);
                debris.SetGoo(cluster);
                spawned++;
            }

            return spawned;
        }

        /// <summary>Share of a shot's goo (<see cref="ShotGoo"/>) thrown on along the shot.</summary>
        public const float SHOT_MAIN = 0.6f;

        /// <summary>Share of a shot's goo splashed out to the sides, half each way.</summary>
        public const float SHOT_SIDES = 0.15f;

        /// <summary>
        /// Launch speed of the spray along the shot, times the goo's own speed: at <see cref="SHOT_ELEVATION"/> its
        /// farthest drops come down about twice as far along the shot as the upward goo rises (the user: twice as far).
        /// </summary>
        public const float SHOT_REACH = 1.35f;

        /// <summary>The spray along the shot rises at this slope (about 25°): it arcs, it does not skim the ground.</summary>
        public const float SHOT_ELEVATION = 0.47f;

        /// <summary>How wide the spray along the shot opens: a cone, narrow at the wound, broad where it lands.</summary>
        public const float SHOT_CONE = 0.32f;

        /// <summary>Extra speed (m/s) a shot gives the torn body parts along its way (<see cref="Chunks"/>' push).</summary>
        public const float SHOT_PUSH = 2.5f;

        /// <summary>
        /// The goo of a body a shell hit (<see cref="Goo"/>), thrown the way the shell flew: most of it on along the shot
        /// in a widening cone that arcs and lands about twice as far as the upward goo rises, a smaller share straight
        /// up as ever, and a little splashed out to the sides. With no way along the ground (<paramref name="shot"/> zero
        /// or straight down: a bomb, a squash) it is the plain upward <see cref="Goo"/>. The same drops in all.
        /// </summary>
        /// <param name="shot">Which way the shell flew (only its part along the ground counts).</param>
        /// <param name="speed">The goo's own launch speed, m/s: the upward share flies at it.</param>
        /// <param name="spread">The upward share's spread, as <see cref="Goo"/>'s.</param>
        public static int ShotGoo(Vector3 at, Vector3 shot, int drops, float speed, float spread,
            IReadOnlyList<Color32> colors, float size, Vector3 inherit = default, float radius = 0.2f)
        {
            Vector3 flat = new(shot.x, 0f, shot.z);
            if (flat.sqrMagnitude < 0.04f * shot.sqrMagnitude || flat.sqrMagnitude < 1e-6f)
                return Goo(at, Vector3.up, drops, speed, spread, colors, size, inherit, radius);

            flat.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            int main = Mathf.RoundToInt(drops * SHOT_MAIN);
            int sides = Mathf.RoundToInt(drops * SHOT_SIDES);
            int up = Mathf.Max(1, drops - main - sides);
            int left = sides / 2;

            int spawned = Goo(at, Vector3.up, up, speed, spread, colors, size, inherit, radius);
            spawned += Goo(at, flat + Vector3.up * SHOT_ELEVATION, main, speed * SHOT_REACH, SHOT_CONE, colors, size, inherit, radius);
            if (left > 0)
                spawned += Goo(at, side + Vector3.up * 0.8f, left, speed * 0.55f, 0.35f, colors, size, inherit, radius);
            if (sides - left > 0)
                spawned += Goo(at, -side + Vector3.up * 0.8f, sides - left, speed * 0.55f, 0.35f, colors, size, inherit, radius);
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
        public static int Chunks(Vector3 at, float height, Vector3 away, IReadOnlyList<Color32> skin, IReadOnlyList<Color32> meat) =>
            Chunks(at, height, away, skin, meat, null);

        /// <param name="partSkin">
        /// The skin of each part, in the order of <see cref="Parts"/> (head, torso, arms, legs; SkinAtlas.ChunkSkins): the
        /// zombie's own colours, its face on the head, its shirt on the torso, its trousers on the legs. A part with none
        /// takes <paramref name="skin"/>.
        /// </param>
        /// <param name="push">
        /// Extra speed (m/s) along <paramref name="away"/>: a shell's shot carries the pieces on its way (<see cref="SHOT_PUSH"/>),
        /// less far than the blood (<see cref="ShotGoo"/>).
        /// </param>
        public static int Chunks(Vector3 at, float height, Vector3 away, IReadOnlyList<Color32> skin, IReadOnlyList<Color32> meat,
            IReadOnlyList<Color32>[] partSkin, float push = 0f)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null || skin == null || skin.Count == 0 || meat == null || meat.Count == 0 || height <= 0f)
                return 0;

            float cube = height / 24f * PIECE_SCALE;
            Vector3 side = Vector3.Cross(Vector3.up, away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward);
            int thrown = 0;
            for (int p = 0; p < Parts.Length; p++)
            {
                (int x, int y, int z, float h, float s) = Parts[p];
                IReadOnlyList<Color32> own = partSkin != null && p < partSkin.Length && partSkin[p] != null && partSkin[p].Count > 0
                    ? partSkin[p]
                    : skin;
                if (!debris.HasGoreRoom(x * y * z))
                    break;
                Vector3 middle = at + Vector3.up * (height * h) + side * (s * cube);
                Vector3 out_ = (middle - (at + Vector3.up * height * 0.6f));
                out_.y = 0f;
                Vector3 velocity = Vector3.up * Random.Range(4f, 6.5f) + out_.normalized * Random.Range(1f, 2.5f) +
                                   away * (Random.Range(1f, 2f) + push * Random.Range(0.7f, 1.2f)) + Random.insideUnitSphere * 0.6f;
                int cluster = debris.BeginGoreCluster(middle, Lifted(velocity), 3f);
                if (cluster < 0)
                    break;
                for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                for (int k = 0; k < z; k++)
                {
                    bool surface = i == 0 || j == 0 || k == 0 || i == x - 1 || j == y - 1 || k == z - 1;
                    Color32 colour = surface && Random.value > 0.2f ? own[Random.Range(0, own.Count)] : meat[Random.Range(0, meat.Count)];
                    Vector3 offset = new Vector3(i - (x - 1) * 0.5f, j - (y - 1) * 0.5f, k - (z - 1) * 0.5f) * cube;
                    debris.AddCube(cluster, middle + offset, cube, colour);
                }

                debris.SetGoo(cluster, true);
                thrown++;
            }

            return thrown;
        }
    }
}
