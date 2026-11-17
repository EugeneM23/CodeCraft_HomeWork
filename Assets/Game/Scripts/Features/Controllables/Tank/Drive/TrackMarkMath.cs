using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Where one track presses into the ground, and how much deeper it presses this frame.</summary>
    public static class TrackMarkMath
    {
        /// <summary>
        /// The track's run on the ground, front to rear: half a wheel ahead of the first grounded
        /// wheel (under the idler), every grounded wheel, half a wheel behind the last (under the
        /// sprocket). Wheels in the air are left out. Returns the run's length; no grounded wheel
        /// gives no points and 0.
        /// </summary>
        public static float Points(IReadOnlyList<Vector3> contacts, IReadOnlyList<bool> grounded, Vector3 forward,
            float extend, List<Vector3> points)
        {
            points.Clear();

            int first = -1;
            int last = -1;
            for (int i = 0; i < contacts.Count; i++)
            {
                if (!grounded[i])
                    continue;
                if (first < 0)
                    first = i;
                last = i;
            }

            if (first < 0)
                return 0f;

            points.Add(contacts[first] + forward * extend);
            for (int i = first; i <= last; i++)
                if (grounded[i])
                    points.Add(contacts[i]);
            points.Add(contacts[last] - forward * extend);

            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        /// <summary>
        /// The end of the run that meets fresh ground: the front going forward (1), the rear in
        /// reverse (-1). Standing still or off the ground there is none (0).
        /// </summary>
        public static int Lead(IReadOnlyList<Vector3> points, float trackSpeed, out Vector3 lead)
        {
            lead = default;
            if (points.Count == 0 || Mathf.Approximately(trackSpeed, 0f))
                return 0;

            if (trackSpeed > 0f)
            {
                lead = points[0];
                return 1;
            }

            lead = points[^1];
            return -1;
        }

        /// <summary>
        /// Whether the ground between last frame's lead and this one's was swept by the track:
        /// only past the <paramref name="frontier"/>, the furthest the lead has got. The lead rocks
        /// back and forth with the hull, and drops back a whole wheel when the end wheel lifts;
        /// ground behind the frontier has been dug already and must not be dug twice. Returns the
        /// strip to dig, from <paramref name="from"/> to <paramref name="lead"/>, and moves the
        /// frontier there.
        ///
        /// A change of leading end or a stop digs nothing and starts the frontier afresh, as does
        /// a leap ahead longer than <paramref name="maxStep"/> (a teleport, not a drive).
        /// </summary>
        public static bool Sweep(ref int frontierEnd, ref Vector3 frontier, int end, Vector3 lead, Vector3 forward,
            float maxStep, out Vector3 from)
        {
            from = frontier;

            if (end == 0 || end != frontierEnd)
            {
                frontierEnd = end;
                frontier = lead;
                return false;
            }

            Vector3 step = lead - frontier;
            step.y = 0f;

            Vector3 travel = forward * end;
            travel.y = 0f;
            travel.Normalize();

            float gain = Vector3.Dot(step, travel);
            if (gain <= 0f)
                return false;

            if (step.sqrMagnitude > maxStep * maxStep)
            {
                frontier = lead;
                return false;
            }

            // Only the gain along the track is new ground. A strip laid along the whole step would
            // turn across the track when the hull steers and dig back over ground behind the lead.
            from = lead - travel * gain;
            frontier = lead;
            return true;
        }
    }
}
