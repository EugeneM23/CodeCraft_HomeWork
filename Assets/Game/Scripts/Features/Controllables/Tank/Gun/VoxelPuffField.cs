using System.Collections.Generic;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: what the small voxel smoke clouds (muzzle smoke, exhaust) feel around them.
    /// - Tanks: a hull's box pushes the cubes out of itself, with the hull's own speed, so a tank
    ///   driving through smoke cuts a wake in it.
    /// - The level: static walls, ground and rubble (VoxelSmoke.WallMask) stop the cubes; they slide
    ///   along and spread over them. Checked by raycasts, each cube every few frames only.
    /// - Shots and blasts: a shell's flight tears a tunnel (Cut), a blast blows the smoke away (Blast).
    /// Clouds call <see cref="Apply"/> on their cubes once a frame.
    /// </summary>
    public static class VoxelPuffField
    {
        /// <summary>Each cube checks the level every this many frames.</summary>
        private const int WALL_STRIDE = 3;

        /// <summary>Gap (m) a cube keeps from a wall or a hull.</summary>
        private const float SKIN = 0.08f;

        /// <summary>Speed (m/s) a cube is thrown out of a tunnel or a hull with.</summary>
        private const float CUT_PUSH = 0.8f; // a hole, not a shove: only the cubes in the tunnel move, and just to its rim
        private const float HULL_PUSH = 1.5f;
        private const float BLAST_PUSH = 9f;

        private struct Hull
        {
            public BoxCollider Box;
            public Rigidbody Body;
        }

        private struct Segment
        {
            public Vector3 From;
            public Vector3 To;
            public float Radius;
            public int Frame;
            public object Source;
            public float Push;
        }

        private struct LiveHull
        {
            public Hull Hull;
            public Vector3 Min;
            public Vector3 Max;
        }

        private static readonly List<Hull> s_hulls = new();

        // Scratch of one Apply (see ApplyCubes); grown once, never shrunk.
        private static readonly List<LiveHull> s_liveHulls = new();
        private static readonly List<Vector3> s_cutMin = new();
        private static readonly List<Vector3> s_cutMax = new();
        private static readonly List<Segment> s_cuts = new();
        private static readonly List<Segment> s_blasts = new();

        public static void AddHull(BoxCollider box, Rigidbody body)
        {
            if (box != null)
                s_hulls.Add(new Hull { Box = box, Body = body });
        }

        public static void RemoveHull(BoxCollider box)
        {
            for (int i = s_hulls.Count - 1; i >= 0; i--)
                if (s_hulls[i].Box == box)
                    s_hulls.RemoveAt(i);
        }

        /// <summary>
        /// A shot flew from <paramref name="from"/> to <paramref name="to"/>: the smoke parts round it.
        /// <paramref name="source"/> is the weapon that fired it: the smoke of that very shot lets it through.
        /// </summary>
        /// <param name="push">Speed (m/s) the smoke is thrown out of the tunnel with; 0 — the default.</param>
        public static void Cut(Vector3 from, Vector3 to, float radius, object source = null, float push = 0f)
        {
            Prune();
            s_cuts.Add(new Segment
            {
                From = from, To = to, Radius = radius, Frame = Time.frameCount, Source = source,
                Push = push > 0f ? push : CUT_PUSH
            });
        }

        /// <summary>A blast: the smoke within <paramref name="radius"/> is blown away from it.</summary>
        public static void Blast(Vector3 point, float radius)
        {
            Prune();
            s_blasts.Add(new Segment { From = point, To = point, Radius = radius, Frame = Time.frameCount });
        }

        /// <summary>
        /// The jet of a shot stops at a wall: how far (m) along <paramref name="direction"/> it gets.
        /// </summary>
        public static float Reach(Vector3 origin, Vector3 direction, float length)
        {
            return Physics.Raycast(origin, direction, out RaycastHit hit, length, VoxelSmoke.WallMask,
                QueryTriggerInteraction.Ignore) && hit.rigidbody == null
                ? Mathf.Max(0.3f, hit.distance - 0.2f)
                : length;
        }

        /// <summary>
        /// Moves and redirects the cubes [0, count) by what is round them: hulls, walls, cuts, blasts.
        /// A cube whose <paramref name="busy"/> entry is above 0 moves on its own and is left alone.
        /// A cube whose <paramref name="shot"/> entry equals <paramref name="ownShot"/> is the smoke of
        /// <paramref name="source"/>'s latest shot: that weapon's own shell flies through it without parting it.
        /// </summary>
        public static void Apply(Vector3[] position, Vector3[] velocity, float[] size, int count, float deltaTime,
            bool walls, bool hulls, float[] busy = null, object source = null, int[] shot = null, int ownShot = 0)
        {
            if (count == 0 || deltaTime <= 0f)
                return;

            using (ApplyMarker.Auto())
                ApplyCubes(position, velocity, size, count, deltaTime, walls, hulls, busy, source, shot, ownShot);
        }

        private static readonly ProfilerMarker ApplyMarker = new("Smoke.PuffField");

        private static void ApplyCubes(Vector3[] position, Vector3[] velocity, float[] size, int count, float deltaTime,
            bool walls, bool hulls, float[] busy, object source, int[] shot, int ownShot)
        {
            int frame = Time.frameCount;
            Prune();
            bool events = s_cuts.Count > 0 || s_blasts.Count > 0;

            // Once a call, not once a cube: the hulls that are on, with their world boxes, and each cut's box. A cube
            // outside a box is out of its reach (the exact test below is the same as before, only skipped then).
            s_liveHulls.Clear();
            if (hulls)
                for (int h = 0; h < s_hulls.Count; h++)
                {
                    BoxCollider box = s_hulls[h].Box;
                    if (box == null || !box.enabled || !box.gameObject.activeInHierarchy)
                        continue;
                    Bounds b = box.bounds;
                    s_liveHulls.Add(new LiveHull { Hull = s_hulls[h], Min = b.min, Max = b.max });
                }

            s_cutMin.Clear();
            s_cutMax.Clear();
            for (int c = 0; c < s_cuts.Count; c++)
            {
                Segment cut = s_cuts[c];
                Vector3 r = new(cut.Radius, cut.Radius, cut.Radius);
                s_cutMin.Add(Vector3.Min(cut.From, cut.To) - r);
                s_cutMax.Add(Vector3.Max(cut.From, cut.To) + r);
            }

            for (int i = 0; i < count; i++)
            {
                if (busy != null && busy[i] > 0f)
                    continue;

                Vector3 p = position[i];
                Vector3 v = velocity[i];
                float half = size != null ? size[i] * 0.5f : 0.1f;

                // The hull's own pad is (half + SKIN) along each of its axes; turned, that reaches up to √3 times as
                // far along a world axis.
                float pad = (half + SKIN) * 1.75f;
                for (int h = 0; h < s_liveHulls.Count; h++)
                {
                    LiveHull live = s_liveHulls[h];
                    if (p.x < live.Min.x - pad || p.y < live.Min.y - pad || p.z < live.Min.z - pad ||
                        p.x > live.Max.x + pad || p.y > live.Max.y + pad || p.z > live.Max.z + pad)
                        continue;
                    PushOutOfHull(live.Hull, ref p, ref v, half);
                }

                if (events)
                {
                    bool own = source != null && shot != null && shot[i] == ownShot;
                    for (int c = 0; c < s_cuts.Count; c++)
                    {
                        Vector3 lo = s_cutMin[c], hi = s_cutMax[c];
                        if (p.x < lo.x || p.y < lo.y || p.z < lo.z || p.x > hi.x || p.y > hi.y || p.z > hi.z)
                            continue;

                        Segment cut = s_cuts[c];
                        if (own && ReferenceEquals(cut.Source, source))
                            continue;

                        Vector3 closest = Closest(cut.From, cut.To, p);
                        Vector3 away = p - closest;
                        float d = away.magnitude;
                        if (d < cut.Radius)
                        {
                            Vector3 dir = d > 1e-4f ? away / d : Random.onUnitSphere;
                            p = closest + dir * cut.Radius;
                            v += dir * cut.Push;
                        }
                    }

                    foreach (Segment blast in s_blasts)
                    {
                        Vector3 away = p - blast.From;
                        float d = away.magnitude;
                        if (d < blast.Radius)
                        {
                            Vector3 dir = d > 1e-4f ? away / d : Random.onUnitSphere;
                            v += dir * (BLAST_PUSH * (1f - d / blast.Radius));
                        }
                    }
                }

                if (walls && (i + frame) % WALL_STRIDE == 0)
                {
                    // Where it will be by its next check: anything static in the way stops it there.
                    float speed = v.magnitude;
                    float reach = speed * deltaTime * WALL_STRIDE + half + SKIN;
                    Vector3 dir = speed > 1e-3f ? v / speed : Vector3.down;
                    if (Physics.Raycast(p, dir, out RaycastHit hit, reach, VoxelSmoke.WallMask,
                            QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                    {
                        // Up to the wall, then along it: the smoke spreads over what it meets.
                        p = hit.point + hit.normal * (half + SKIN);
                        float into = Vector3.Dot(v, hit.normal);
                        if (into < 0f)
                            v -= hit.normal * into;
                        v *= 0.85f;
                    }
                }

                position[i] = p;
                velocity[i] = v;
            }
        }

        private static void PushOutOfHull(Hull hull, ref Vector3 p, ref Vector3 v, float half)
        {
            BoxCollider box = hull.Box;
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy)
                return;

            Transform t = box.transform;
            Vector3 scale = t.lossyScale;
            Vector3 local = t.InverseTransformPoint(p) - box.center;
            Vector3 ext = box.size * 0.5f;
            // The cube's own half edge and a gap, in the box's local units.
            Vector3 pad = new((half + SKIN) / Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                (half + SKIN) / Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                (half + SKIN) / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            Vector3 limit = ext + pad;
            if (Mathf.Abs(local.x) >= limit.x || Mathf.Abs(local.y) >= limit.y || Mathf.Abs(local.z) >= limit.z)
                return;

            // Out through the nearest face, measured in metres.
            float dx = (limit.x - Mathf.Abs(local.x)) * Mathf.Abs(scale.x);
            float dy = (limit.y - Mathf.Abs(local.y)) * Mathf.Abs(scale.y);
            float dz = (limit.z - Mathf.Abs(local.z)) * Mathf.Abs(scale.z);
            Vector3 normalLocal;
            if (dx <= dy && dx <= dz)
            {
                local.x = Mathf.Sign(local.x) * limit.x;
                normalLocal = new Vector3(Mathf.Sign(local.x), 0f, 0f);
            }
            else if (dy <= dz)
            {
                local.y = Mathf.Sign(local.y) * limit.y;
                normalLocal = new Vector3(0f, Mathf.Sign(local.y), 0f);
            }
            else
            {
                local.z = Mathf.Sign(local.z) * limit.z;
                normalLocal = new Vector3(0f, 0f, Mathf.Sign(local.z));
            }

            p = t.TransformPoint(local + box.center);
            Vector3 normal = t.TransformDirection(normalLocal);
            Vector3 hullVelocity = hull.Body != null ? hull.Body.GetPointVelocity(p) : Vector3.zero;
            // Dragged along with the hull a little and shoved aside at least HULL_PUSH: a wake.
            v = Vector3.Lerp(v, hullVelocity, 0.3f);
            float outward = Vector3.Dot(v - hullVelocity, normal);
            if (outward < HULL_PUSH)
                v += normal * (HULL_PUSH - outward);
        }

        private static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f)
                return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        }

        /// <summary>Events last this frame and the next (shots come in the fixed step), then go.</summary>
        private static void Prune()
        {
            int oldest = Time.frameCount - 1;
            for (int i = s_cuts.Count - 1; i >= 0; i--)
                if (s_cuts[i].Frame < oldest)
                    s_cuts.RemoveAt(i);
            for (int i = s_blasts.Count - 1; i >= 0; i--)
                if (s_blasts[i].Frame < oldest)
                    s_blasts.RemoveAt(i);
        }
    }
}
