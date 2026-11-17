using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Plain geometry of the canvas, no canvas needed (tests ask it directly): where a new node goes (the project's rule,
    /// 14.11.2026: nodes never land on each other), where a link label docked at its pin stands (step 90, C2), and the
    /// point of the Brain's frame each of its wires leaves from, with a lane of its own (step 90, C10).
    /// </summary>
    public static class GraphLayout
    {
        /// <summary>Rounds of sliding at most (each round passes at least one node).</summary>
        private const int MOST_ROUNDS = 256;

        /// <summary>
        /// The place nearest to where the node was put — straight down from it — where it covers none of the taken places,
        /// with gap pixels kept free around each. Taken places with no size (not laid out yet) are not in the way.
        /// </summary>
        public static Vector2 FreeSpot(Rect wanted, IReadOnlyList<Rect> taken, float gap)
        {
            Rect at = wanted;
            for (int round = 0; round < MOST_ROUNDS; round++)
            {
                bool moved = false;
                for (int i = 0; i < taken.Count; i++)
                {
                    Rect other = taken[i];
                    if (other.width < 1f || other.height < 1f || !Overlaps(at, other, gap))
                        continue;

                    at.y = other.yMax + gap;            // below it
                    moved = true;
                }

                if (!moved)
                    return at.position;
            }

            return at.position;
        }

        /// <summary>The two places come nearer than gap to each other (or cover each other).</summary>
        public static bool Overlaps(Rect a, Rect b, float gap) =>
            a.xMin < b.xMax + gap && b.xMin < a.xMax + gap && a.yMin < b.yMax + gap && b.yMin < a.yMax + gap;

        // ---------- link labels docked at their pins (C2) ----------

        /// <summary>Pixels between a pin and the label docked at it.</summary>
        public const float LINK_OFFSET = 6f;

        /// <summary>Pixels between two labels docked at one pin.</summary>
        public const float LINK_GAP = 2f;

        /// <summary>
        /// Where the index-th link label docked at a pin stands (its top-left corner): just right of the pin, level with it,
        /// each next label under the one before — a pin 24 px high never reaches the next pin's row with the first label.
        /// </summary>
        public static Vector2 LinkSpot(Rect pin, int index, float height, float gap = LINK_GAP, float offset = LINK_OFFSET) =>
            new(pin.xMax + offset, pin.center.y - height * 0.5f + Mathf.Max(0, index) * (height + gap));

        // ---------- the Brain's wires (C10) ----------

        /// <summary>Where a wire of the Brain leaves its frame and the lane its upright stretch runs in.</summary>
        public readonly struct BrainExit
        {
            public BrainExit(FrameSide side, Vector2 point, float lane, float turn)
            {
                Side = side;
                Point = point;
                Lane = lane;
                Turn = turn;
            }

            /// <summary>The side of the frame the wire leaves.</summary>
            public FrameSide Side { get; }

            /// <summary>The point on that side.</summary>
            public Vector2 Point { get; }

            /// <summary>The x of the wire's upright stretch (a lane of its own: wires never run on one line).</summary>
            public float Lane { get; }

            /// <summary>A wire that leaves the top or the bottom and has to go round: the y it turns at; else unused.</summary>
            public float Turn { get; }
        }

        /// <summary>How far below a decision's top its In pin is, for the order of the wires (the real pin is used to draw).</summary>
        public const float IN_PIN_Y = 33f;

        /// <summary>
        /// The point of the frame each wire of the Brain leaves from, one per decision (in the order given), and the lane
        /// of each: a wire leaves the side that faces its decision; the wires of one side are spread evenly along it in the
        /// order of their decisions (no two cross where they leave); each gets an upright lane of its own between the frame
        /// and the decisions, so no two wires run on one line. stored — the point the user put a wire at (null — none):
        /// kept as it is, and the others spread round it.
        /// </summary>
        public static BrainExit[] BrainExits(Rect frame, IReadOnlyList<Rect> decisions, float spacing,
            IReadOnlyList<(FrameSide Side, float Along)?> stored = null)
        {
            int count = decisions?.Count ?? 0;
            var exits = new BrainExit[count];
            if (count == 0)
                return exits;

            var sides = new FrameSide[count];
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                (FrameSide Side, float Along)? own = stored != null && i < stored.Count ? stored[i] : null;
                sides[i] = own is { Side: not FrameSide.None } placed ? placed.Side : Facing(frame, decisions[i]);
                if (own is { Side: not FrameSide.None } kept)
                    points[i] = PointOn(frame, kept.Side, kept.Along);
            }

            // The wires of one side that nobody placed: spread along it in the order their decisions stand in, round the
            // ones placed on that side — the side is cut into as many even points as it has wires, each placed wire takes
            // the point nearest to its own, the free ones the rest (no free wire leaves from a placed one's point)
            foreach (FrameSide side in new[] { FrameSide.Right, FrameSide.Left, FrameSide.Top, FrameSide.Bottom })
            {
                var free = new List<int>();
                var placedAlong = new List<float>();
                for (int i = 0; i < count; i++)
                {
                    bool placed = stored != null && i < stored.Count && stored[i] is { Side: not FrameSide.None };
                    if (sides[i] != side)
                        continue;

                    if (placed)
                        placedAlong.Add(Mathf.Clamp01(stored[i].Value.Along));
                    else
                        free.Add(i);
                }

                bool upright = side is FrameSide.Left or FrameSide.Right;
                free.Sort((a, b) =>
                {
                    float left = upright ? InOf(decisions[a]).y : decisions[a].center.x;
                    float right = upright ? InOf(decisions[b]).y : decisions[b].center.x;
                    return left != right ? left.CompareTo(right) : a.CompareTo(b);
                });

                int slots = free.Count + placedAlong.Count;
                var taken = new bool[slots];
                foreach (float along in placedAlong)
                {
                    int nearest = -1;
                    float best = float.MaxValue;
                    for (int k = 0; k < slots; k++)
                    {
                        float off = Mathf.Abs((k + 1f) / (slots + 1f) - along);
                        if (!taken[k] && off < best)
                        {
                            best = off;
                            nearest = k;
                        }
                    }

                    if (nearest >= 0)
                        taken[nearest] = true;
                }

                for (int k = 0, j = 0; k < slots && j < free.Count; k++)
                {
                    if (!taken[k])
                        points[free[j++]] = PointOn(frame, side, (k + 1f) / (slots + 1f));
                }
            }

            // The lanes, side by side: a wire going up runs nearer the frame the higher it leaves, one going down — the
            // lower it leaves (so none crosses another), every wire in a lane of its own
            var lanes = new float[count];
            var turns = new float[count];
            foreach (FrameSide side in new[] { FrameSide.Right, FrameSide.Left, FrameSide.Top, FrameSide.Bottom })
            {
                var group = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if (sides[i] == side)
                        group.Add(i);
                }

                if (group.Count == 0)
                    continue;

                var up = new List<int>();
                var down = new List<int>();
                foreach (int i in group)
                {
                    if (InOf(decisions[i]).y < points[i].y)
                        up.Add(i);
                    else
                        down.Add(i);
                }

                up.Sort((a, b) => points[a].y != points[b].y ? points[a].y.CompareTo(points[b].y) : a.CompareTo(b));
                down.Sort((a, b) => points[a].y != points[b].y ? points[b].y.CompareTo(points[a].y) : a.CompareTo(b));
                var order = new List<int>(up);
                order.AddRange(down);

                switch (side)
                {
                    case FrameSide.Right:
                    {
                        float nearest = float.MaxValue;
                        foreach (int i in group)
                            nearest = Mathf.Min(nearest, InOf(decisions[i]).x);
                        float room = nearest - frame.xMax;
                        float step = room > 0f ? Mathf.Min(spacing, room / (order.Count + 1f)) : spacing;
                        for (int k = 0; k < order.Count; k++)
                            lanes[order[k]] = frame.xMax + step * (k + 1);
                        break;
                    }
                    case FrameSide.Left:
                    {
                        float farthest = float.MaxValue;
                        foreach (int i in group)
                            farthest = Mathf.Min(farthest, InOf(decisions[i]).x);
                        for (int k = 0; k < order.Count; k++)
                            lanes[order[k]] = farthest - spacing * (k + 1);
                        break;
                    }
                    default:
                    {
                        // Up or down out of the frame: straight to the decision's row when its pin is to the right, else
                        // round, in a lane of its own left of the pins
                        float farthest = float.MaxValue;
                        foreach (int i in group)
                            farthest = Mathf.Min(farthest, InOf(decisions[i]).x);
                        for (int k = 0; k < order.Count; k++)
                        {
                            int i = order[k];
                            bool straight = points[i].x <= InOf(decisions[i]).x - spacing;
                            lanes[i] = straight ? points[i].x : farthest - spacing * (k + 1);
                            turns[i] = side == FrameSide.Top
                                ? frame.yMin - spacing * (k + 1)
                                : frame.yMax + spacing * (k + 1);
                        }

                        break;
                    }
                }
            }

            for (int i = 0; i < count; i++)
                exits[i] = new BrainExit(sides[i], points[i], lanes[i], turns[i]);
            return exits;
        }

        /// <summary>
        /// The corners of a wire of the Brain, from its exit to the decision's In pin, at right angles: out of the frame to
        /// its lane, along the lane to the pin's row, into the pin from its left.
        /// </summary>
        public static List<Vector2> BrainWirePath(BrainExit exit, Vector2 into)
        {
            Vector2 from = exit.Point;
            var path = new List<Vector2> { from };
            switch (exit.Side)
            {
                case FrameSide.Left:
                case FrameSide.Right:
                    if (Mathf.Abs(into.y - from.y) >= 0.5f || Mathf.Abs(exit.Lane - from.x) >= 0.5f)
                    {
                        path.Add(new Vector2(exit.Lane, from.y));
                        path.Add(new Vector2(exit.Lane, into.y));
                    }

                    break;

                default:
                    if (Mathf.Abs(exit.Lane - from.x) < 0.5f)
                    {
                        path.Add(new Vector2(from.x, into.y));          // straight up or down to the pin's row
                    }
                    else
                    {
                        path.Add(new Vector2(from.x, exit.Turn));
                        path.Add(new Vector2(exit.Lane, exit.Turn));
                        path.Add(new Vector2(exit.Lane, into.y));
                    }

                    break;
            }

            path.Add(into);
            return path;
        }

        /// <summary>The side of the frame that faces a decision: the one it lies beyond (by its middle when it overlaps).</summary>
        public static FrameSide Facing(Rect frame, Rect decision)
        {
            if (decision.xMin >= frame.xMax)
                return FrameSide.Right;
            if (decision.xMax <= frame.xMin)
                return FrameSide.Left;
            if (decision.center.y < frame.yMin)
                return FrameSide.Top;
            if (decision.center.y > frame.yMax)
                return FrameSide.Bottom;
            return decision.center.x >= frame.center.x ? FrameSide.Right : FrameSide.Left;
        }

        /// <summary>The point on a side of the box: along — 0 at its top / left end, 1 at the other.</summary>
        public static Vector2 PointOn(Rect box, FrameSide side, float along)
        {
            along = Mathf.Clamp01(along);
            return side switch
            {
                FrameSide.Left => new Vector2(box.xMin, Mathf.Lerp(box.yMin, box.yMax, along)),
                FrameSide.Right => new Vector2(box.xMax, Mathf.Lerp(box.yMin, box.yMax, along)),
                FrameSide.Top => new Vector2(Mathf.Lerp(box.xMin, box.xMax, along), box.yMin),
                FrameSide.Bottom => new Vector2(Mathf.Lerp(box.xMin, box.xMax, along), box.yMax),
                _ => box.center
            };
        }

        /// <summary>The side of the box nearest to a point, and how far along that side the point lies (0 — its top / left end).</summary>
        public static FrameSide SideOf(Rect box, Vector2 at, out float along)
        {
            float left = Mathf.Abs(at.x - box.xMin);
            float right = Mathf.Abs(at.x - box.xMax);
            float top = Mathf.Abs(at.y - box.yMin);
            float bottom = Mathf.Abs(at.y - box.yMax);
            float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            if (nearest == left || nearest == right)
            {
                along = Mathf.Clamp01(Mathf.InverseLerp(box.yMin, box.yMax, at.y));
                return nearest == left ? FrameSide.Left : FrameSide.Right;
            }

            along = Mathf.Clamp01(Mathf.InverseLerp(box.xMin, box.xMax, at.x));
            return nearest == top ? FrameSide.Top : FrameSide.Bottom;
        }

        /// <summary>The point lies on the rim of the box: within rim pixels of one of its edges.</summary>
        public static bool OnRim(Rect box, Vector2 at, float rim)
        {
            var outer = new Rect(box.xMin - rim, box.yMin - rim, box.width + 2f * rim, box.height + 2f * rim);
            var inner = new Rect(box.xMin + rim, box.yMin + rim, box.width - 2f * rim, box.height - 2f * rim);
            return outer.Contains(at) && (inner.width <= 0f || inner.height <= 0f || !inner.Contains(at));
        }

        // Where a decision's In pin is, near enough to order the wires by
        private static Vector2 InOf(Rect decision) => new(decision.xMin, decision.yMin + IN_PIN_Y);
    }
}
