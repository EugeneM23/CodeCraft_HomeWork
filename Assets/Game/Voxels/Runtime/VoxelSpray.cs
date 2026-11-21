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
            // Long strands, like a sticky splash: about seven drops a strand, stringing out from fast tip to slow tail.
            int strands = Mathf.Max(1, drops / 7);
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
                    Vector3 velocity = inherit + heading * (top * Mathf.Lerp(1f, 0.25f, t));
                    Color32 c = Random.value < 0.3f ? colors[Random.Range(0, colors.Count)] : color;
                    if (debris.GooDrop(start + heading * (0.03f * k), Lifted(velocity), c, size * Random.Range(0.8f, 1.3f)) < 0)
                        return spawned;
                    spawned++;
                }
            }

            // No blobs (the user): the many-coloured clumps of cubes balling up are gone; only the strands fly.
            int blobs = GOO_BLOBS ? Mathf.Max(1, drops / 8) : 0;
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

        /// <summary>Clumps of several cubes in the goo (<see cref="Goo"/>): off (the user, the many-coloured balls).</summary>
        private const bool GOO_BLOBS = false;

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
        public const float SHOT_PUSH = 5f; // was 2.5: the user doubled a shell's throw

        /// <summary>A shell's hit throws its whole spray (along, up, sides) this much faster than other goo.</summary>
        public const float SHOT_FORCE = 2f;

        /// <summary>An HE shell's hit throws the upward and sideways shares of its spray this much faster again.</summary>
        public const float HE_SPLASH = 2f;

        /// <summary>How high (m) a shot's goo sheet (<see cref="ShotGoo"/>'s sheet) rises at power 1.</summary>
        public const float SHEET_HEIGHT = 1.6f;

        /// <summary>The size of a shot spray's upward drops against the rest (the ones that hang in the air).</summary>
        public const float UP_DROP_SIZE = 0.5f;

        /// <summary>
        /// The goo of a body a shell hit (<see cref="Goo"/>), thrown the way the shell flew: most of it on along the shot
        /// in a widening cone that arcs and lands about twice as far as the upward goo rises, a smaller share straight
        /// up as ever, and a little splashed out to the sides. With no way along the ground (<paramref name="shot"/> zero
        /// or straight down: a bomb, a squash) it is the plain upward <see cref="Goo"/>. The same drops in all.
        /// </summary>
        /// <param name="shot">Which way the shell flew (only its part along the ground counts).</param>
        /// <param name="speed">The goo's own launch speed, m/s: the upward share flies at it.</param>
        /// <param name="spread">The upward share's spread, as <see cref="Goo"/>'s.</param>
        /// <param name="splash">The upward and sideways shares fly this much faster (an HE shell: 2); the share along
        /// the shot is not touched.</param>
        /// <param name="sheet">
        /// A shell's hit or kill on a body: the middle of the spray rises as one twisting sheet of goo (VoxelDebris.GooSheet,
        /// <see cref="SHEET_HEIGHT"/> times this: the shell's power) and the upward drops are half as many round it. 0: no sheet.
        /// </param>
        public static int ShotGoo(Vector3 at, Vector3 shot, int drops, float speed, float spread,
            IReadOnlyList<Color32> colors, float size, Vector3 inherit = default, float radius = 0.2f, float splash = 1f, float sheet = 0f)
        {
            Vector3 flat = new(shot.x, 0f, shot.z);
            if (flat.sqrMagnitude < 0.04f * shot.sqrMagnitude || flat.sqrMagnitude < 1e-6f)
                return Goo(at, Vector3.up, drops, speed, spread, colors, size, inherit, radius);

            flat.Normalize();
            speed *= SHOT_FORCE;
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            int main = Mathf.RoundToInt(drops * SHOT_MAIN);
            int sides = Mathf.RoundToInt(drops * SHOT_SIDES);
            int up = Mathf.Max(1, drops - main - sides);
            // The middle of the spray as a liquid sheet (the user's splash): round it stay half the upward drops.
            VoxelWorld world = sheet > 0f ? VoxelWorld.Instance : null;
            // With the sheet, it alone goes up (the user): none of the upward drops round it.
            if (world != null && world.Debris != null && world.Debris.GooSheet(at, flat, SHEET_HEIGHT, colors, sheet))
                up = 0;
            int left = sides / 2;

            // The upward share hangs in the air before it falls: its drops are half the size (the same count and spread).
            int spawned = up > 0 ? Goo(at, Vector3.up, up, speed * splash, spread, colors, size * UP_DROP_SIZE, inherit, radius) : 0;
            spawned += Goo(at, flat + Vector3.up * SHOT_ELEVATION, main, speed * SHOT_REACH, SHOT_CONE, colors, size, inherit, radius);
            if (left > 0)
                spawned += Goo(at, side + Vector3.up * 0.8f, left, speed * 0.55f * splash, 0.35f, colors, size, inherit, radius);
            if (sides - left > 0)
                spawned += Goo(at, -side + Vector3.up * 0.8f, sides - left, speed * 0.55f * splash, 0.35f, colors, size, inherit, radius);
            return spawned;
        }

        /// <summary>
        /// A pop under the hull is as strong as an HE shell's hit on a body (ShellHitSparksUseCase.EmitFlesh): as many
        /// drops and bits, as big, as fast (28.6 m/s); only flat, out to every side.
        /// </summary>
        public const int UNDER_HULL_DROPS = 30;
        public const int UNDER_HULL_BITS = 14;
        public const float UNDER_HULL_SPEED = 28.6f;
        public const float UNDER_HULL_DROP_SIZE = 0.11f;
        public const float UNDER_HULL_BIT_SIZE = 0.15f;

        /// <summary>
        /// The pop's speed against <see cref="UNDER_HULL_SPEED"/>: the user, a third as far (a flat throw from a hand over the
        /// ground carries about as speed^1.6, so speed × 0.5 lands about a third out).
        /// </summary>
        private const float UNDER_HULL_REACH = 0.5f;

        /// <summary>How many jets a pop throws (random each time), the user's splatter: a few long streaks, not even fans.</summary>
        private const int UNDER_HULL_JETS_MIN = 3;
        private const int UNDER_HULL_JETS_MAX = 7;

        /// <summary>Share of the drops that stay in a clot round where the body burst (slow, wide).</summary>
        private const float UNDER_HULL_CLOT = 0.3f;

        /// <summary>Bits of flesh among the crush blood: off (the user, the many-coloured cubes).</summary>
        private const bool UNDER_HULL_FLESH = false;

        /// <summary>A fan's rise against its speed out: flat, skimming the ground.</summary>
        private const float UNDER_HULL_LIFT = 0.06f;

        /// <summary>How far past the hull's footprint (m) a fan starts, and how high over the ground: below the belly.</summary>
        private const float UNDER_HULL_OUT = 0.25f;
        private const float UNDER_HULL_HEIGHT = 0.12f;

        /// <summary>
        /// A body popped under a tank's hull (the user: like an HE shell's blast, out from under the belly to every side):
        /// blood and bits of flesh in flat fans all round, each starting just outside the hull's footprint where its way
        /// leaves it, a hand over the ground, so nothing comes up through the hull or lands on it; they skim out and splash
        /// the ground metres round (goo: stretched strands, sticks or soaks in where it lands).
        /// </summary>
        /// <param name="at">The body (its feet), under the hull.</param>
        /// <param name="hullCenter">The hull box's centre, its rotation and half size (its footprint is its XZ).</param>
        /// <param name="groundY">The ground under the body.</param>
        /// <returns>Drops and blobs that flew.</returns>
        public static int UnderHull(Vector3 at, Vector3 hullCenter, Quaternion hullRotation, Vector3 hullHalf, float groundY,
            int drops, int bits, float speed, IReadOnlyList<Color32> blood, IReadOnlyList<Color32> flesh, float dropSize, float bitSize,
            Vector3 inherit = default)
        {
            Quaternion inverse = Quaternion.Inverse(hullRotation);
            Vector3 local = inverse * (at - hullCenter);
            speed *= UNDER_HULL_REACH;

            // A splatter, not a star (the user's pictures): a few jets of random length, most of them bunched to one side
            // the way a burst squirts, one or two strays the other way, and a clot of slow drops round the burst.
            int jets = Random.Range(UNDER_HULL_JETS_MIN, UNDER_HULL_JETS_MAX + 1);
            float main = Random.Range(0f, 360f);
            float lobe = Random.Range(40f, 140f);
            int clot = Mathf.RoundToInt(drops * UNDER_HULL_CLOT);
            int jetDrops = drops - clot;
            System.Span<float> weight = stackalloc float[UNDER_HULL_JETS_MAX];
            float total = 0f;
            for (int k = 0; k < jets; k++)
            {
                weight[k] = Random.Range(0.3f, 1.6f);
                total += weight[k];
            }

            int spawned = 0;
            for (int k = 0; k < jets; k++)
            {
                bool stray = k > 0 && Random.value < 0.25f;
                float angle = stray ? main + 180f + Random.Range(-90f, 90f) : main + Random.Range(-0.5f, 0.5f) * lobe;
                Vector3 out_ = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 start = Exit(local, out_);
                Vector3 way = out_ + Vector3.up * (UNDER_HULL_LIFT * Random.Range(0.5f, 2f));

                // A heavier jet carries more drops and flies further: long streaks next to short spurts.
                float share = weight[k] / total;
                int n = Mathf.Max(2, Mathf.RoundToInt(jetDrops * share));
                float reach = Mathf.Lerp(0.45f, 1.15f, Mathf.InverseLerp(0.3f, 1.6f, weight[k])) * Random.Range(0.85f, 1.15f);
                spawned += Goo(start, way, n, speed * reach, Random.Range(0.03f, 0.12f), blood,
                    dropSize * Random.Range(0.8f, 1.3f), inherit, 0.05f);

                // No coloured bits of flesh (the user): only blood, unless UNDER_HULL_FLESH is switched back on.
                int m = UNDER_HULL_FLESH ? Mathf.RoundToInt(bits * share) : 0;
                if (m > 0 && flesh != null && flesh.Count > 0)
                    spawned += Goo(start, way, m, speed * reach * Random.Range(0.5f, 0.8f), 0.15f, flesh, bitSize, inherit, 0.05f);
            }

            // The clot: slow drops in a puddle round the burst, wider on the jets' side.
            for (int c = 0; c < clot; c += 3)
            {
                Vector3 out_ = Quaternion.Euler(0f, main + Random.Range(-120f, 120f), 0f) * Vector3.forward;
                spawned += Goo(Exit(local, out_), out_ + Vector3.up * UNDER_HULL_LIFT, Mathf.Min(3, clot - c),
                    speed * Random.Range(0.08f, 0.25f), 0.5f, blood, dropSize * Random.Range(1f, 1.6f), inherit, 0.15f);
            }

            return spawned;

            // Where a way out leaves the hull's footprint, a hand over the ground, just outside it.
            Vector3 Exit(Vector3 bodyLocal, Vector3 dir)
            {
                Vector3 p = hullCenter + hullRotation * FootprintExit(bodyLocal, inverse * dir, hullHalf, out _);
                return new Vector3(p.x, groundY + UNDER_HULL_HEIGHT, p.z) + dir * UNDER_HULL_OUT;
            }
        }

        /// <summary>
        /// Where a way out of the hull's footprint from <paramref name="local"/> (hull space) leaves it: the point on the
        /// footprint's edge along <paramref name="way"/> (hull space, flat). Outside the footprint already: the point itself.
        /// </summary>
        private static Vector3 FootprintExit(Vector3 local, Vector3 way, Vector3 half, out float travel)
        {
            travel = 0f;
            if (Mathf.Abs(local.x) >= half.x || Mathf.Abs(local.z) >= half.z)
                return local;
            float tx = Mathf.Abs(way.x) > 1e-4f ? ((way.x > 0f ? half.x : -half.x) - local.x) / way.x : float.MaxValue;
            float tz = Mathf.Abs(way.z) > 1e-4f ? ((way.z > 0f ? half.z : -half.z) - local.z) / way.z : float.MaxValue;
            travel = Mathf.Max(0f, Mathf.Min(tx, tz));
            return local + new Vector3(way.x, 0f, way.z) * travel;
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
