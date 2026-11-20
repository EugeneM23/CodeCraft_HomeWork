using System.Collections.Generic;
using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The attack places round the tank's hull for the skinned zombies (<see cref="ZombieChaseBehaviour"/>): the same
    /// ring as the horde's (<see cref="HordeHullRing"/>), cut into places a skinned body wide. Once a frame the first
    /// zombie that asks hands the places out: a zombie near the hull keeps the place it had, then the ones without take
    /// the free place nearest to them round the ring, the nearest to the hull first. A zombie with no free place waits a
    /// body further out. Main thread; no allocations after the first frame.
    /// </summary>
    public static class ZombieRing
    {
        /// <summary>Distance of the attackers' middles from the hull box, m (the motor keeps them at least 0.6 off).</summary>
        public const float STANDOFF = 0.75f;

        /// <summary>Width of a place, m: two zombies keep 1.1 m between their middles (<see cref="ZombieChaseBehaviour.KEEP_APART"/>), even across a corner of the ring.</summary>
        public const float SPACING = 1.3f;

        /// <summary>A zombie takes a place this close to the hull, m, and gives it up 2 m further out.</summary>
        public const float RANGE = 6f;

        private const float KEEP_MARGIN = 2f;

        private static readonly List<ZombieChaseBehaviour> s_members = new();
        private static readonly int[] s_owner = new int[HordeHullRing.MAX_SLOTS];
        private static readonly float[] s_hull = new float[256];
        private static int s_frame = -1;
        private static int s_count;
        private static float2 s_half;
        private static float s_length;
        private static bool s_active;

        /// <summary>Places on the ring this frame (0: no tank).</summary>
        public static int Count => s_active ? s_count : 0;

        public static float Length => s_length;

        public static float2 Half => s_half;

        /// <summary>The skinned zombies that chase now (they also keep apart by this list).</summary>
        internal static List<ZombieChaseBehaviour> Members => s_members;

        internal static void Join(ZombieChaseBehaviour member)
        {
            if (!s_members.Contains(member))
                s_members.Add(member);
        }

        internal static void Leave(ZombieChaseBehaviour member)
        {
            s_members.Remove(member);
            member.RingSlot = -1;
        }

        /// <summary>Hands the places out for this frame, once (whoever asks first).</summary>
        internal static void Update(PlayerTank tank)
        {
            int frame = Time.frameCount;
            if (frame == s_frame)
                return;
            s_frame = frame;
            Assign(tank);
        }

        /// <summary>The places are handed out again at the next ask, even in the same frame (EditMode tests: the frame count stands still).</summary>
        public static void Invalidate() => s_frame = -1;

        /// <summary>Hands the places out now (tests: twice in a frame).</summary>
        internal static void Assign(PlayerTank tank)
        {
            Vector3 centre = default, right = default, forward = default;
            Vector2 h = default;
            s_active = tank != null && tank.TryGetHullFrame(out centre, out right, out forward, out h);
            if (!s_active)
            {
                for (int m = 0; m < s_members.Count; m++)
                    s_members[m].RingSlot = -1;
                return;
            }

            s_half = new float2(h.x, h.y);
            s_length = HordeHullRing.Length(s_half, STANDOFF);
            s_count = HordeHullRing.SlotCount(s_half, STANDOFF, SPACING);
            for (int k = 0; k < s_count; k++)
                s_owner[k] = -1;

            int members = Mathf.Min(s_members.Count, s_hull.Length);
            for (int m = 0; m < members; m++)
                s_hull[m] = tank.DistanceToHull(s_members[m].Position);

            // Keep.
            for (int m = 0; m < members; m++)
            {
                ZombieChaseBehaviour z = s_members[m];
                int slot = z.RingSlot;
                if (slot < 0)
                    continue;
                if (slot >= s_count || !z.CanHoldPlace || s_owner[slot] != -1 || s_hull[m] > RANGE + KEEP_MARGIN)
                {
                    z.RingSlot = -1;
                    continue;
                }

                s_owner[slot] = m;
            }

            // Take: the front row first.
            Claim(centre, right, forward, members, RANGE * 0.5f);
            Claim(centre, right, forward, members, RANGE);
        }

        /// <summary>A world point to hull space (XZ: right, forward) of the frame the places were handed out in.</summary>
        public static float2 ToLocal(Vector3 point, Vector3 centre, Vector3 right, Vector3 forward)
        {
            Vector3 d = point - centre;
            return new float2(Vector3.Dot(d, right), Vector3.Dot(d, forward));
        }

        public static Vector3 ToWorld(float2 local, Vector3 centre, Vector3 right, Vector3 forward, float y) =>
            new(centre.x + right.x * local.x + forward.x * local.y, y, centre.z + right.z * local.x + forward.z * local.y);

        private static void Claim(Vector3 centre, Vector3 right, Vector3 forward, int members, float range)
        {
            for (int m = 0; m < members; m++)
            {
                ZombieChaseBehaviour z = s_members[m];
                if (z.RingSlot >= 0 || !z.CanHoldPlace || s_hull[m] > range)
                    continue;

                float t = HordeHullRing.Param(ToLocal(z.Position, centre, right, forward), s_half, STANDOFF);
                int nearest = HordeHullRing.NearestSlot(t, s_count, s_length);
                float lean = HordeHullRing.Delta(HordeHullRing.SlotParam(nearest, s_count, s_length), t, s_length);
                for (int d = 0; d <= s_count / 2; d++)
                {
                    int first = lean >= 0f ? d : -d;
                    if (TryTake(z, m, nearest + first) || (d > 0 && TryTake(z, m, nearest - first)))
                        break;
                }
            }
        }

        private static bool TryTake(ZombieChaseBehaviour z, int member, int slot)
        {
            int k = (slot % s_count + s_count) % s_count;
            if (s_owner[k] != -1)
                return false;
            s_owner[k] = member;
            z.RingSlot = k;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_members.Clear();
            s_frame = -1;
            s_active = false;
        }
    }
}
