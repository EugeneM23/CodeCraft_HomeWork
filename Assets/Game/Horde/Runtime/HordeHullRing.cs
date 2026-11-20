using Unity.Mathematics;

namespace Game.Horde
{
    /// <summary>
    /// The ring of attack places round the tank's hull: the hull box (flat, its half width and half length
    /// <c>half</c>) grown by <c>standoff</c> on every side, with round corners. A point of the ring is its arc length
    /// <c>t</c> from the front end of the right side, going round toward +Z (counter-clockwise seen from above).
    /// The ring is cut into <see cref="SlotCount"/> equal places, one body each, so the attackers stand round the hull
    /// side by side instead of in one heap. Hull space: X right, Z forward, the hull's middle at the origin.
    /// Pure math, no state: Burst jobs and the main thread use the same.
    /// </summary>
    public static class HordeHullRing
    {
        /// <summary>Places on one ring at most (a bigger hull gets wider places).</summary>
        public const int MAX_SLOTS = 96;

        /// <summary>Length of the ring round a hull of <paramref name="half"/> grown by <paramref name="standoff"/>.</summary>
        public static float Length(float2 half, float standoff) =>
            4f * (half.x + half.y) + 2f * math.PI * math.max(standoff, 0f);

        /// <summary>How many places of <paramref name="spacing"/> fit on the ring (1..<see cref="MAX_SLOTS"/>).</summary>
        public static int SlotCount(float2 half, float standoff, float spacing) =>
            math.clamp((int)math.floor(Length(half, standoff) / math.max(spacing, 0.01f)), 1, MAX_SLOTS);

        /// <summary>Arc length of the middle of place <paramref name="slot"/> of <paramref name="count"/>.</summary>
        public static float SlotParam(int slot, int count, float length) => (slot + 0.5f) * length / math.max(count, 1);

        /// <summary>The place nearest to arc length <paramref name="t"/>.</summary>
        public static int NearestSlot(float t, int count, float length)
        {
            float k = Wrap(t, length) / math.max(length, 1e-4f) * count - 0.5f;
            int slot = (int)math.round(k);
            return ((slot % count) + count) % count;
        }

        /// <summary><paramref name="t"/> brought into 0..<paramref name="length"/>.</summary>
        public static float Wrap(float t, float length) => length > 0f ? t - math.floor(t / length) * length : 0f;

        /// <summary>Shortest way round the ring from <paramref name="from"/> to <paramref name="to"/> (signed, ±half the ring).</summary>
        public static float Delta(float from, float to, float length)
        {
            float d = Wrap(to - from, length);
            return d > length * 0.5f ? d - length : d;
        }

        /// <summary>
        /// Arc length of the ring point nearest to <paramref name="p"/> (hull space XZ). A point inside the hull goes to
        /// its nearest side.
        /// </summary>
        public static float Param(float2 p, float2 half, float standoff)
        {
            float s = math.max(standoff, 0f);
            float a = 0.5f * math.PI * s;
            float off1 = 2f * half.y;
            float off2 = off1 + a;
            float off3 = off2 + 2f * half.x;
            float off4 = off3 + a;
            float off5 = off4 + 2f * half.y;
            float off6 = off5 + a;
            float off7 = off6 + 2f * half.x;

            if (p.x >= half.x && p.y >= half.y)
                return off1 + s * Angle(p.y - half.y, p.x - half.x, 0f);
            if (p.x <= -half.x && p.y >= half.y)
                return off3 + s * Angle(p.y - half.y, p.x + half.x, 0.5f * math.PI);
            if (p.x <= -half.x && p.y <= -half.y)
                return off5 + s * Angle(p.y + half.y, p.x + half.x, math.PI);
            if (p.x >= half.x && p.y <= -half.y)
                return off7 + s * Angle(p.y + half.y, p.x - half.x, 1.5f * math.PI);

            float ex = math.abs(p.x) - half.x;
            float ez = math.abs(p.y) - half.y;
            if (ex >= ez)
                return p.x >= 0f ? math.clamp(p.y + half.y, 0f, off1) : off4 + math.clamp(half.y - p.y, 0f, 2f * half.y);
            return p.y >= 0f ? off2 + math.clamp(half.x - p.x, 0f, 2f * half.x) : off6 + math.clamp(p.x + half.x, 0f, 2f * half.x);
        }

        /// <summary>The ring point at arc length <paramref name="t"/> (hull space XZ) and its outward normal.</summary>
        public static float2 PointAt(float t, float2 half, float standoff, out float2 normal)
        {
            float s = math.max(standoff, 0f);
            float a = 0.5f * math.PI * s;
            float length = Length(half, s);
            t = Wrap(t, length);

            float side = 2f * half.y;
            if (t <= side)
            {
                normal = new float2(1f, 0f);
                return new float2(half.x + s, -half.y + t);
            }

            t -= side;
            if (t <= a)
                return Corner(new float2(half.x, half.y), 0f, t, s, out normal);

            t -= a;
            side = 2f * half.x;
            if (t <= side)
            {
                normal = new float2(0f, 1f);
                return new float2(half.x - t, half.y + s);
            }

            t -= side;
            if (t <= a)
                return Corner(new float2(-half.x, half.y), 0.5f * math.PI, t, s, out normal);

            t -= a;
            side = 2f * half.y;
            if (t <= side)
            {
                normal = new float2(-1f, 0f);
                return new float2(-half.x - s, half.y - t);
            }

            t -= side;
            if (t <= a)
                return Corner(new float2(-half.x, -half.y), math.PI, t, s, out normal);

            t -= a;
            side = 2f * half.x;
            if (t <= side)
            {
                normal = new float2(0f, -1f);
                return new float2(-half.x + t, -half.y - s);
            }

            t -= side;
            return Corner(new float2(half.x, -half.y), 1.5f * math.PI, math.min(t, a), s, out normal);
        }

        /// <summary>
        /// Where a body at <paramref name="p"/> (hull space XZ) heads for its place at arc length <paramref name="slot"/>
        /// of the ring <paramref name="standoff"/> off the hull: straight at it when it is within <paramref name="direct"/>
        /// along the ring; further, round the shorter way along the lane <paramref name="travel"/> off the hull, a point
        /// <paramref name="direct"/> ahead at a time — round the hull, not through it, and behind the ones already
        /// standing at their places, not through them.
        /// </summary>
        public static float2 Approach(float2 p, float slot, float2 half, float standoff, float travel, float direct)
        {
            float length = Length(half, standoff);
            float2 place = PointAt(slot, half, standoff, out _);
            if (math.abs(Delta(Param(p, half, standoff), slot, length)) <= direct)
                return place;

            float lane = Length(half, travel);
            float from = Param(p, half, travel);
            float d = Delta(from, Param(place, half, travel), lane);
            return PointAt(from + math.sign(d) * math.min(math.abs(d), direct), half, travel, out _);
        }

        /// <summary><see cref="Approach(float2,float,float2,float,float,float)"/> with the lane on the ring itself.</summary>
        public static float2 Approach(float2 p, float slot, float2 half, float standoff, float direct)
        {
            float length = Length(half, standoff);
            float t = Param(p, half, standoff);
            float d = Delta(t, slot, length);
            float step = math.abs(d) <= direct ? d : math.sign(d) * direct;
            return PointAt(t + step, half, standoff, out _);
        }

        // Angle of (x, y) from `start`, 0..pi/2 (a quarter turn of one corner)
        private static float Angle(float y, float x, float start)
        {
            if (math.abs(x) < 1e-6f && math.abs(y) < 1e-6f)
                return 0f;
            float angle = math.atan2(y, x) - start;
            angle -= math.floor(angle / (2f * math.PI)) * (2f * math.PI);
            return math.min(angle, 0.5f * math.PI);
        }

        private static float2 Corner(float2 centre, float start, float t, float s, out float2 normal)
        {
            float angle = start + (s > 0f ? t / s : 0f);
            math.sincos(angle, out float sin, out float cos);
            normal = new float2(cos, sin);
            return centre + normal * s;
        }
    }
}
